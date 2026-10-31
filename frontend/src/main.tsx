import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App.tsx'
import { HttpError } from './api/client.ts'
import { queryKeys } from './lib/queryKeys.ts'
import './index.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // A 401 means the session is gone; retrying cannot fix it.
      retry: (failureCount, error) =>
        !(error instanceof HttpError && error.status === 401) && failureCount < 1,
      refetchOnWindowFocus: false,
    },
  },
  // Any 401 outside the login flow drops the cached session, which makes
  // AuthGate navigate to the login page.
  queryCache: new QueryCache({
    onError: (error) => {
      if (error instanceof HttpError && error.status === 401) {
        queryClient.setQueryData(queryKeys.authMe, null)
      }
    },
  }),
  mutationCache: new MutationCache({
    onError: (error) => {
      if (error instanceof HttpError && error.status === 401) {
        queryClient.setQueryData(queryKeys.authMe, null)
      }
    },
  }),
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>,
)
