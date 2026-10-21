# SharpChat frontend

React + TypeScript + Vite client for SharpChat. See the root `README.md` and `DECISIONS.md` for the project overview.

## Commands

| Command                | What it does                                 |
| ---------------------- | -------------------------------------------- |
| `npm run dev`          | Dev server with hot reload                   |
| `npm run build`        | Type check and production build into `dist/` |
| `npm run lint`         | ESLint                                       |
| `npm run format`       | Prettier (write)                             |
| `npm run format:check` | Prettier (check only)                        |

## Styling

Design tokens (colors, fonts, spacing) live in `src/styles/tokens.css` as CSS variables. Components use CSS Modules and only reference the variables, so changing the accent color is a one-line change.
