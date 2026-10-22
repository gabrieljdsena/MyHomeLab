import { useCallback, useEffect, useRef, useState } from 'react'

export type PtyStatus = 'connecting' | 'open' | 'closed' | 'error'

export interface PtyExitInfo {
  code: number | null
  reason?: string
}

interface UseTerminalSocketOptions {
  shell: string
  sessionId?: string
  onOutput: (data: string) => void
  onExit: (info: PtyExitInfo) => void
  onAttached?: (sessionId: string) => void
  onErrorMessage: (message: string) => void
}

export function buildPtyUrl(shell: string, cols: number, rows: number, sessionId?: string): string {
  const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:'
  const params = new URLSearchParams({
    shell,
    cols: String(Math.round(cols)),
    rows: String(Math.round(rows)),
  })
  if (sessionId) {
    params.set('session', sessionId)
  }
  return `${protocol}//${window.location.host}/api/terminal/ws?${params}`
}

export function useTerminalSocket({
  shell,
  sessionId,
  onOutput,
  onExit,
  onAttached,
  onErrorMessage,
}: UseTerminalSocketOptions) {
  const [status, setStatus] = useState<PtyStatus>('connecting')
  const [error, setError] = useState<string | null>(null)
  const wsRef = useRef<WebSocket | null>(null)
  const handlersRef = useRef({ sessionId, onOutput, onExit, onAttached, onErrorMessage })
  useEffect(() => {
    handlersRef.current = { sessionId, onOutput, onExit, onAttached, onErrorMessage }
  })

  const closeSocket = useCallback(() => {
    const ws = wsRef.current
    wsRef.current = null
    if (ws && (ws.readyState === WebSocket.OPEN || ws.readyState === WebSocket.CONNECTING)) {
      try {
        ws.close(1000, 'client closed')
      } catch {
        // ignore
      }
    }
  }, [])

  const connect = useCallback(
    (cols: number, rows: number) => {
      closeSocket()
      setStatus('connecting')
      setError(null)

      let ws: WebSocket
      try {
        ws = new WebSocket(buildPtyUrl(shell, cols, rows, handlersRef.current.sessionId))
      } catch (err) {
        setStatus('error')
        setError(err instanceof Error ? err.message : 'Failed to open terminal connection.')
        return
      }

      wsRef.current = ws

      ws.onopen = () => {
        if (wsRef.current === ws) {
          setStatus('open')
        }
      }

      ws.onmessage = (event: MessageEvent) => {
        if (wsRef.current !== ws) return
        if (typeof event.data !== 'string') return

        let frame: { type?: string; data?: string; code?: number; message?: string; session?: string }
        try {
          frame = JSON.parse(event.data) as typeof frame
        } catch {
          return
        }

        if (frame.type === 'output' && typeof frame.data === 'string') {
          handlersRef.current.onOutput(frame.data)
        } else if (frame.type === 'attached' && typeof frame.session === 'string') {
          handlersRef.current.onAttached?.(frame.session)
        } else if (frame.type === 'exit') {
          handlersRef.current.onExit({ code: typeof frame.code === 'number' ? frame.code : null })
        } else if (frame.type === 'error' && typeof frame.message === 'string') {
          handlersRef.current.onErrorMessage(frame.message)
        }
      }

      ws.onerror = () => {
        if (wsRef.current === ws) {
          setStatus('error')
          setError('Terminal connection failed. The API may be unreachable or busy.')
        }
      }

      ws.onclose = (event: CloseEvent) => {
        if (wsRef.current !== ws) return
        wsRef.current = null
        if (event.code === 1008 || event.code === 1003) {
          setStatus('error')
          setError(event.reason || 'Terminal session was rejected.')
        } else if (event.code !== 1000 && event.code !== 1005) {
          setStatus('error')
          setError(
            `Terminal connection closed unexpectedly (code ${event.code}${event.reason ? `: ${event.reason}` : ''}). Try Reconnect.`,
          )
        } else {
          setStatus((prev) => (prev === 'error' ? prev : 'closed'))
        }
      }
    },
    [shell, closeSocket],
  )

  const sendInput = useCallback((data: string) => {
    const ws = wsRef.current
    if (!ws || ws.readyState !== WebSocket.OPEN || !data) return
    ws.send(JSON.stringify({ type: 'input', data: data.slice(0, 65536) }))
  }, [])

  const sendResize = useCallback((cols: number, rows: number) => {
    const ws = wsRef.current
    if (!ws || ws.readyState !== WebSocket.OPEN) return
    if (!(cols > 0 && rows > 0)) return
    ws.send(JSON.stringify({ type: 'resize', cols: Math.round(cols), rows: Math.round(rows) }))
  }, [])

  useEffect(() => closeSocket, [closeSocket])

  return { status, error, connect, disconnect: closeSocket, sendInput, sendResize }
}
