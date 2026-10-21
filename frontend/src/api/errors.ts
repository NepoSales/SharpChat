type ProblemDetails = {
  title?: string
  detail?: string
  status?: number
  code?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly code: string | undefined
  readonly fieldErrors: Record<string, string[]>

  constructor(status: number, problem: ProblemDetails = {}) {
    super(problem.detail ?? problem.title ?? `Request failed with status ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.code = problem.code
    this.fieldErrors = Object.fromEntries(
      Object.entries(problem.errors ?? {}).map(([field, messages]) => [
        field.charAt(0).toLowerCase() + field.slice(1),
        messages,
      ]),
    )
  }

  static async fromResponse(response: Response): Promise<ApiError> {
    const isProblem = response.headers.get('content-type')?.includes('json') ?? false
    const problem = isProblem ? ((await response.json()) as ProblemDetails) : undefined
    return new ApiError(response.status, problem)
  }
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  if (error instanceof TypeError) return 'Could not reach the server. Check your connection.'
  return 'Something went wrong. Please try again.'
}
