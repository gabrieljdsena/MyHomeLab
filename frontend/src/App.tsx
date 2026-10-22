import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { AuthGate } from './features/auth/AuthGate'
import { LoginPage } from './features/auth/LoginPage'
import { SetupPage } from './features/auth/SetupPage'
import { Dashboard } from './pages/Dashboard'
import { Files } from './pages/Files'
import { Logs } from './pages/Logs'
import { Machines } from './pages/Machines'
import { Manage } from './pages/Manage'
import { Postgres } from './pages/Postgres'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/setup" element={<SetupPage />} />
        <Route
          element={
            <AuthGate>
              <Layout />
            </AuthGate>
          }
        >
          <Route index element={<Dashboard />} />
          <Route path="/manage" element={<Manage />} />
          <Route path="/machines" element={<Machines />} />
          <Route path="/files" element={<Files />} />
          <Route path="/logs" element={<Logs />} />
          <Route path="/postgres" element={<Postgres />} />
          {/* Rendered by Layout's keep-alive workspace, not by the outlet. */}
          <Route path="/terminal" />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
