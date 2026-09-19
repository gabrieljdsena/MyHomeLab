import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { Dashboard } from './pages/Dashboard'
import { Manage } from './pages/Manage'
import { Terminal } from './pages/Terminal'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<Dashboard />} />
          <Route path="/manage" element={<Manage />} />
          <Route path="/terminal" element={<Terminal />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}