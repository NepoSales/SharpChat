using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SharpChat.Application.Messages;
using SharpChat.Domain.Attachments;
using SharpChat.Infrastructure.Storage;
using SharpChat.IntegrationTests.Auth;
using SharpChat.IntegrationTests.Conversations;
using SharpChat.IntegrationTests.Infrastructure;
using SharpChat.IntegrationTests.Messaging;

namespace SharpChat.IntegrationTests.Attachments;

[Collection(ApiTestGroup.Name)]
public sealed class AttachmentTests(SharpChatApiFactory factory)
{
    [Fact]
    public async Task An_image_is_sent_with_a_message_and_members_download_it()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobChat = await bob.ConnectToChatAsync(factory);

        var image = await alice.UploadOkAsync(conversation.Id, "screenshot.png", AttachmentApi.Png);
        var sent = await aliceChat.SendWithAttachmentsAsync(conversation.Id, "look", [image.Id]);

        Assert.Null(sent.Error);
        var received = await bobChat.NextMessageAsync();
        var attachment = Assert.Single(received.Attachments);
        Assert.Equal(
            (image.Id, "screenshot.png", "image/png", AttachmentKind.Image, (long)AttachmentApi.Png.Length),
            (attachment.Id, attachment.FileName, attachment.ContentType, attachment.Kind, attachment.SizeBytes));

        var download = await bob.DownloadAsync(conversation.Id, image.Id);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(AttachmentApi.Png, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("sandbox", download.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task History_and_single_message_include_attachments_in_upload_order()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        await using var chat = await alice.ConnectToChatAsync(factory);
        var pdf = await alice.UploadOkAsync(conversation.Id, "spec.pdf", AttachmentApi.Pdf);
        var log = await alice.UploadOkAsync(conversation.Id, "build.log", "error: boom\n"u8.ToArray());

        var sent = (await chat.SendWithAttachmentsAsync(conversation.Id, string.Empty, [log.Id, pdf.Id])).Data!;

        var page = await alice.Client.GetFromJsonAsync<MessagePage>($"/conversations/{conversation.Id}/messages", TestUser.Json);
        var single = await alice.Client.GetFromJsonAsync<MessageDto>(
            $"/conversations/{conversation.Id}/messages/{sent.Id}", TestUser.Json);
        Assert.Equal([log.Id, pdf.Id], sent.Attachments.Select(a => a.Id));
        Assert.Equal([pdf.Id, log.Id], Assert.Single(page!.Items).Attachments.Select(a => a.Id));
        Assert.Equal([pdf.Id, log.Id], single!.Attachments.Select(a => a.Id));
        Assert.Equal(string.Empty, sent.Content);
    }

    [Fact]
    public async Task Documents_download_as_attachments_with_the_original_name()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        var notes = await alice.UploadOkAsync(conversation.Id, "notas da reunião.md", "# hi\n"u8.ToArray());

        var download = await alice.DownloadAsync(conversation.Id, notes.Id);

        Assert.Equal("text/plain; charset=utf-8", download.Content.Headers.ContentType?.ToString());
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("notas da reunião.md", download.Content.Headers.ContentDisposition?.FileNameStar);
    }

    [Theory]
    [InlineData("page.html", "<html><script>alert(1)</script></html>")]
    [InlineData("logo.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("tool.exe", "MZ\u0090\u0000")]
    [InlineData("fake.png", "not really a png")]
    public async Task Unsupported_or_disguised_files_are_rejected(string fileName, string content)
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");

        var response = await alice.UploadAsync(conversation.Id, fileName, System.Text.Encoding.UTF8.GetBytes(content), "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_png_named_as_text_is_stored_as_an_image()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");

        var uploaded = await alice.UploadOkAsync(conversation.Id, "readme.txt", AttachmentApi.Png);

        Assert.Equal(("image/png", AttachmentKind.Image), (uploaded.ContentType, uploaded.Kind));
    }

    [Fact]
    public async Task Files_over_the_size_limit_are_rejected()
    {
        await using var small = factory.WithWebHostBuilder(b => b.UseSetting("Chat:MaxAttachmentBytes", "1024"));
        var alice = await TestUser.CreateAsync(small);
        var conversation = await alice.CreateConversationOkAsync("team");

        var tooBig = new byte[1025];
        AttachmentApi.Png.CopyTo(tooBig, 0);
        var response = await alice.UploadAsync(conversation.Id, "big.png", tooBig);
        var empty = await alice.UploadAsync(conversation.Id, "empty.txt", []);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Bodies_over_the_hard_limit_are_rejected_before_storage()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");

        var response = await alice.UploadAsync(conversation.Id, "huge.png", new byte[26 * 1024 * 1024]);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge });
        Assert.False(Directory.Exists(Path.Combine(factory.StorageRoot, conversation.Id.ToString("N"))));
    }

    [Fact]
    public async Task Outsiders_cannot_upload_or_download()
    {
        var alice = await TestUser.CreateAsync(factory);
        var outsider = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        await using var chat = await alice.ConnectToChatAsync(factory);
        var image = await alice.UploadOkAsync(conversation.Id, "a.png", AttachmentApi.Png);
        await chat.SendWithAttachmentsAsync(conversation.Id, "x", [image.Id]);

        var upload = await outsider.UploadAsync(conversation.Id, "b.png", AttachmentApi.Png);
        var download = await outsider.DownloadAsync(conversation.Id, image.Id);

        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
    }

    [Fact]
    public async Task Pending_uploads_are_private_to_the_uploader()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team", bob);
        var image = await alice.UploadOkAsync(conversation.Id, "draft.png", AttachmentApi.Png);

        Assert.Equal(HttpStatusCode.OK, (await alice.DownloadAsync(conversation.Id, image.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DownloadAsync(conversation.Id, image.Id)).StatusCode);
    }

    [Fact]
    public async Task Attachments_of_someone_else_or_another_conversation_cannot_be_sent()
    {
        var alice = await TestUser.CreateAsync(factory);
        var bob = await TestUser.CreateAsync(factory);
        var team = await alice.CreateConversationOkAsync("team", bob);
        var other = await alice.CreateConversationOkAsync("other");
        await using var aliceChat = await alice.ConnectToChatAsync(factory);
        await using var bobChat = await bob.ConnectToChatAsync(factory);
        var alicesImage = await alice.UploadOkAsync(team.Id, "a.png", AttachmentApi.Png);
        var otherImage = await alice.UploadOkAsync(other.Id, "b.png", AttachmentApi.Png);

        var stolen = await bobChat.SendWithAttachmentsAsync(team.Id, "mine now", [alicesImage.Id]);
        var crossed = await aliceChat.SendWithAttachmentsAsync(team.Id, "wrong room", [otherImage.Id]);
        var missing = await aliceChat.SendWithAttachmentsAsync(team.Id, "ghost", [Guid.NewGuid()]);

        Assert.Equal("attachment_unavailable", stolen.Error?.Code);
        Assert.Equal("attachment_unavailable", crossed.Error?.Code);
        Assert.Equal("attachment_unavailable", missing.Error?.Code);
    }

    [Fact]
    public async Task An_attachment_is_linked_once_and_resending_the_same_message_is_idempotent()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        await using var chat = await alice.ConnectToChatAsync(factory);
        var image = await alice.UploadOkAsync(conversation.Id, "a.png", AttachmentApi.Png);
        var messageId = Guid.NewGuid();

        var first = await chat.SendWithAttachmentsAsync(conversation.Id, "once", [image.Id], messageId);
        var retry = await chat.SendWithAttachmentsAsync(conversation.Id, "once", [image.Id], messageId);
        var reused = await chat.SendWithAttachmentsAsync(conversation.Id, "twice", [image.Id]);

        Assert.Null(first.Error);
        Assert.Null(retry.Error);
        Assert.Equal(image.Id, Assert.Single(retry.Data!.Attachments).Id);
        Assert.Equal("attachment_unavailable", reused.Error?.Code);
    }

    [Fact]
    public async Task More_than_the_limit_per_message_is_rejected()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        await using var chat = await alice.ConnectToChatAsync(factory);
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add((await alice.UploadOkAsync(conversation.Id, $"{i}.png", AttachmentApi.Png)).Id);
        }

        var response = await chat.SendWithAttachmentsAsync(conversation.Id, "too many", ids);

        Assert.Equal("validation_failed", response.Error?.Code);
    }

    [Fact]
    public async Task A_message_without_text_or_attachments_is_still_rejected()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        await using var chat = await alice.ConnectToChatAsync(factory);

        var response = await chat.SendWithAttachmentsAsync(conversation.Id, " ", []);

        Assert.Equal("validation_failed", response.Error?.Code);
    }

    [Fact]
    public async Task Deleting_a_message_hides_its_attachments_and_cleanup_removes_the_files()
    {
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        await using var chat = await alice.ConnectToChatAsync(factory);
        var image = await alice.UploadOkAsync(conversation.Id, "a.png", AttachmentApi.Png);
        var sent = (await chat.SendWithAttachmentsAsync(conversation.Id, "oops", [image.Id])).Data!;
        var file = Path.Combine(factory.StorageRoot, conversation.Id.ToString("N"), image.Id.ToString("N"));
        Assert.True(File.Exists(file));

        var deleted = await chat.DeleteAsync(sent.Id);
        var download = await alice.DownloadAsync(conversation.Id, image.Id);
        await RunCleanupAsync(factory.Services);

        Assert.Empty(deleted.Data!.Attachments);
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task Cleanup_removes_pending_uploads_only_after_their_lifetime()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var frozen = factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        var alice = await TestUser.CreateAsync(frozen);
        var conversation = await alice.CreateConversationOkAsync("team");
        await using var chat = await alice.ConnectToChatAsync(frozen);
        var abandoned = await alice.UploadOkAsync(conversation.Id, "abandoned.png", AttachmentApi.Png);
        var sentImage = await alice.UploadOkAsync(conversation.Id, "sent.png", AttachmentApi.Png);
        await chat.SendWithAttachmentsAsync(conversation.Id, "kept", [sentImage.Id]);

        await RunCleanupAsync(frozen.Services);
        Assert.Equal(HttpStatusCode.OK, (await alice.DownloadAsync(conversation.Id, abandoned.Id)).StatusCode);

        clock.Advance(TimeSpan.FromHours(25));
        await RunCleanupAsync(frozen.Services);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.DownloadAsync(conversation.Id, abandoned.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alice.DownloadAsync(conversation.Id, sentImage.Id)).StatusCode);
    }

    [Fact]
    public async Task A_file_uploaded_on_one_instance_downloads_from_the_other()
    {
        await using var instanceB = factory.WithWebHostBuilder(builder => builder.UseSetting("InstanceName", "instance-b"));
        var alice = await TestUser.CreateAsync(factory);
        var conversation = await alice.CreateConversationOkAsync("team");
        var image = await alice.UploadOkAsync(conversation.Id, "a.png", AttachmentApi.Png);

        var onB = instanceB.CreateClient();
        onB.DefaultRequestHeaders.Authorization = alice.Client.DefaultRequestHeaders.Authorization;
        var download = await onB.GetAsync($"/conversations/{conversation.Id}/attachments/{image.Id}");

        Assert.Equal(AttachmentApi.Png, await download.Content.ReadAsByteArrayAsync());
    }

    private static async Task RunCleanupAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AttachmentCleanup>().RunOnceAsync(CancellationToken.None);
    }
}
