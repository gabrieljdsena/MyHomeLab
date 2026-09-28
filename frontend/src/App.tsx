import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { Dashboard } from './pages/Dashboard'
import { Logs } from './pages/Logs'
import { Machines } from './pages/Machines'
import { Manage } from './pages/Manage'
import { Postgres } from './pages/Postgres'
import { Terminal } from './pages/Terminal'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<Dashboard />} />
          <Route path="/manage" element={<Manage />} />
          <Route path="/machines" element={<Machines />} />
          <Route path="/logs" element={<Logs />} />
          <Route path="/postgres" element={<Postgres />} />
          <Route path="/terminal" element={<Terminal />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}