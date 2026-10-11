import type { DiscoveredDevice, MachineSummary, NetworkTopology } from '../lib/types'
import { formatLatency } from '../lib/format'
import { Icon } from './Icon'

const W = 760
const PER_ROW = 6
const ROW_GAP = 96
const NODE_R = 17

const linkColor: Record<MachineSummary['reachability'], string> = {
  online: 'var(--color-up)',
  offline: 'var(--color-down)',
  unknown: 'var(--color-muted)',
}

function nicIcon(kind: string): string {
  if (kind === 'wireless') return 'wifi'
  if (kind === 'wired') return 'lan'
  return 'device_unknown'
}

function truncate(name: string, max = 14): string {
  return name.length > max ? `${name.slice(0, max - 1)}…` : name
}

function layoutRow(count: number, top: number): { x: number; y: number }[] {
  const cols = Math.min(count, PER_ROW)
  const gap = Math.min(124, (W - 120) / Math.max(cols - 1, 1))
  const startX = W / 2 - (gap * (cols - 1)) / 2
  return Array.from({ length: count }, (_, i) => ({
    x: startX + (i % PER_ROW) * gap,
    y: top + Math.floor(i / PER_ROW) * ROW_GAP,
  }))
}

export function NetworkMap({
  topology,
  discovered,
  selectedId,
  onSelect,
}: {
  topology: NetworkTopology
  discovered: DiscoveredDevice[]
  selectedId: string | null
  onSelect: (id: string | null) => void
}) {
  const nodes = topology.nodes
  const unknowns = discovered.filter((d) => d.matchedMachineId === null)
  const shown = unknowns.slice(0, 24)
  const hidden = unknowns.length - shown.length

  const hubY = topology.gateway ? 196 : 120
  // All hub→node links fan out from a "port" dot below the hub's own name/IP/badges,
  // so no line ever crosses hub text. Node rows start below the port.
  const portY = hubY + 100
  const nodesTop = hubY + 170
  const nodeSlots = layoutRow(Math.max(nodes.length, 1), nodesTop)
  const lastNodeY =
    nodes.length === 0 ? nodesTop : nodesTop + (Math.ceil(nodes.length / PER_ROW) - 1) * ROW_GAP
  const discoveredTop = nodes.length === 0 ? hubY + 140 : lastNodeY + 124
  const discoveredSlots = layoutRow(Math.max(shown.length, 1), discoveredTop)
  const lastDiscoveredY =
    shown.length === 0 ? discoveredTop : discoveredTop + (Math.ceil(shown.length / PER_ROW) - 1) * ROW_GAP
  const height =
    (shown.length > 0 ? lastDiscoveredY + 44 : nodes.length > 0 ? lastNodeY + 40 : hubY + 108 + 20) + 16
  const routerY = 60

  return (
    <div>
      <svg
        viewBox={`0 0 ${W} ${height}`}
        className="h-auto w-full"
        role="img"
        aria-label={`Network map of ${topology.hub.hostName} with ${nodes.length} machines`}
      >
        {topology.gateway && (
          <line
            x1={W / 2}
            y1={routerY + 58}
            x2={W / 2}
            y2={hubY - 26}
            stroke="var(--color-accent)"
            strokeWidth={2}
            opacity={0.8}
          />
        )}
        <circle cx={W / 2} cy={portY} r={3.5} fill="var(--color-surface-2)" stroke="var(--color-muted)" strokeWidth={1.5} />
        {nodes.map((node, i) => {
          const slot = nodeSlots[i]
          if (!slot) return null
          const dimmed = !node.isEnabled
          return (
            <line
              key={`link-${node.id}`}
              x1={W / 2}
              y1={portY}
              x2={slot.x}
              y2={slot.y - NODE_R}
              stroke={linkColor[node.reachability]}
              strokeWidth={selectedId === node.id ? 2.5 : 1.5}
              strokeDasharray={node.reachability === 'unknown' || dimmed ? '5 4' : undefined}
              opacity={dimmed ? 0.3 : selectedId === node.id ? 0.9 : 0.45}
            />
          )
        })}

        {topology.gateway && (
          <g>
            <title>
              {`Router${topology.gateway.hostname ? ` · ${topology.gateway.hostname}` : ''}\n${topology.gateway.ipAddress}${topology.gateway.macAddress ? `\n${topology.gateway.macAddress}` : ''}\nDefault gateway for this LAN`}
            </title>
            <circle cx={W / 2} cy={routerY} r={20} fill="var(--color-accent-soft)" stroke="var(--color-accent)" strokeWidth={2} />
            <text x={W / 2} y={routerY} textAnchor="middle" dominantBaseline="central" fontSize={20} fill="var(--color-accent)" className="material-symbols-outlined">
              router
            </text>
            <text x={W / 2} y={routerY + 36} textAnchor="middle" fontSize={12} fontWeight={600} fill="var(--color-text)">
              Router
            </text>
            <text x={W / 2} y={routerY + 50} textAnchor="middle" fontSize={10} fill="var(--color-muted)">
              {topology.gateway.hostname ?? topology.gateway.ipAddress}
            </text>
          </g>
        )}

        <g>
          <title>
            {topology.hub.hostName}
            {topology.hub.interfaces.map((nic) => `\n${nic.name}: ${nic.ipv4} (${nic.subnet})`).join('')}
          </title>
          <circle cx={W / 2} cy={hubY} r={22} fill="var(--color-accent-soft)" stroke="var(--color-accent)" strokeWidth={2} />
          <text x={W / 2} y={hubY} textAnchor="middle" dominantBaseline="central" fontSize={22} fill="var(--color-accent)" className="material-symbols-outlined">
            dns
          </text>
          <text x={W / 2} y={hubY + 40} textAnchor="middle" fontSize={12} fontWeight={600} fill="var(--color-text)">
            {truncate(topology.hub.hostName, 20)}
          </text>
          <text x={W / 2} y={hubY + 54} textAnchor="middle" fontSize={10} fill="var(--color-muted)">
            {topology.hub.interfaces.length > 0 ? topology.hub.interfaces[0]?.ipv4 : 'this hub'}
            {topology.hub.interfaces.length > 1 ? ` +${topology.hub.interfaces.length - 1}` : ''}
          </text>
          {topology.hub.interfaces.map((nic, i) => {
            const spread = (topology.hub.interfaces.length - 1) * 11
            return (
              <text
                key={`${nic.name}-${nic.ipv4}`}
                x={W / 2 - spread + i * 22}
                y={hubY + 84}
                textAnchor="middle"
                fontSize={13}
                fill="var(--color-muted)"
                className="material-symbols-outlined"
              >
                <title>{`${nic.name}: ${nic.kind === 'wireless' ? 'Wi-Fi' : nic.kind === 'wired' ? 'cabled' : 'unknown link'} · ${nic.ipv4}`}</title>
                {nicIcon(nic.kind)}
              </text>
            )
          })}
        </g>

        {nodes.length === 0 && shown.length === 0 && (
          <text x={W / 2} y={hubY + 108} textAnchor="middle" fontSize={12} fill="var(--color-muted)">
            No machines registered yet — add one or wait for discovery.
          </text>
        )}

        {nodes.map((node, i) => {
          const slot = nodeSlots[i]
          if (!slot) return null
          const selected = selectedId === node.id
          const dimmed = !node.isEnabled
          const tooltip = `${node.name}\n${node.hostname}${node.ipAddress && node.ipAddress !== node.hostname ? ` (${node.ipAddress})` : ''}\n${node.reachability}${node.lastLatencyMs !== null ? ` · ${formatLatency(node.lastLatencyMs)}` : ''}`
          return (
            // eslint-disable-next-line jsx-a11y/no-static-element-interactions
            <g
              key={node.id}
              onClick={() => onSelect(selected ? null : node.id)}
              style={{ cursor: 'pointer' }}
              opacity={dimmed ? 0.55 : 1}
            >
              <title>{tooltip}</title>
              {selected && (
                <circle cx={slot.x} cy={slot.y} r={NODE_R + 6} fill="none" stroke="var(--color-accent)" strokeWidth={2} strokeDasharray="4 3" />
              )}
              <circle cx={slot.x} cy={slot.y} r={NODE_R} fill="var(--color-surface-2)" stroke={linkColor[node.reachability]} strokeWidth={2} />
              <text x={slot.x} y={slot.y} textAnchor="middle" dominantBaseline="central" fontSize={19} fill="var(--color-text)" className="material-symbols-outlined">
                {node.icon}
              </text>
              <circle cx={slot.x + 13} cy={slot.y - 13} r={5} fill={linkColor[node.reachability]} stroke="var(--color-surface)" strokeWidth={1.5} />
              <text x={slot.x} y={slot.y + 33} textAnchor="middle" fontSize={11} fontWeight={selected ? 700 : 400} fill={selected ? 'var(--color-accent)' : 'var(--color-text)'}>
                {truncate(node.name)}
              </text>
            </g>
          )
        })}

        {shown.length > 0 && (
          <text x={24} y={discoveredTop - 52} fontSize={11} fontWeight={600} fill="var(--color-muted)">
            Discovered — not registered{hidden > 0 ? ` (+${hidden} more below)` : ''}
          </text>
        )}
        {shown.map((device, i) => {
          const slot = discoveredSlots[i]
          if (!slot) return null
          const label = device.hostname ?? device.ipAddress
          const glyph = device.suggestedIcon ?? 'device_unknown'
          return (
            <g key={device.ipAddress} opacity={0.8}>
              <title>{`${label}${device.deviceType ? ` · ${device.deviceType}` : ''}\n${device.ipAddress}${device.macAddress ? `\n${device.macAddress}` : ''}\nlast seen ${new Date(device.lastSeenUtc).toLocaleString()}`}</title>
              <circle cx={slot.x} cy={slot.y} r={16} fill="transparent" stroke="var(--color-muted)" strokeWidth={1.5} strokeDasharray="3 2" />
              <text x={slot.x} y={slot.y} textAnchor="middle" dominantBaseline="central" fontSize={18} fill="var(--color-muted)" className="material-symbols-outlined">
                {glyph}
              </text>
              <text x={slot.x} y={slot.y + 31} textAnchor="middle" fontSize={10} fill="var(--color-muted)">
                {truncate(label, 16)}
              </text>
            </g>
          )
        })}
      </svg>

      <div className="flex flex-wrap items-center gap-x-4 gap-y-1 border-t border-border/60 px-4 py-2.5 text-[11px] text-muted">
        <span className="inline-flex items-center gap-1.5">
          <Icon name="router" className="text-[14px] text-accent" /> router
        </span>
        <span className="inline-flex items-center gap-1.5">
          <span className="size-2 rounded-full bg-up" /> online
        </span>
        <span className="inline-flex items-center gap-1.5">
          <span className="size-2 rounded-full bg-down" /> offline
        </span>
        <span className="inline-flex items-center gap-1.5">
          <span className="size-2 rounded-full bg-muted" /> unknown
        </span>
        <span className="inline-flex items-center gap-1.5">
          <Icon name="lan" className="text-[14px]" /> cabled
        </span>
        <span className="inline-flex items-center gap-1.5">
          <Icon name="wifi" className="text-[14px]" /> wi-fi
        </span>
        <span className="ml-auto hidden sm:inline">Hover a node for details · click to select</span>
      </div>
    </div>
  )
}
