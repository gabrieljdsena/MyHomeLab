'use strict';

// MyHomeLab PTY sidecar: spawns one interactive shell via node-pty and
// bridges it to NDJSON lines over stdio for the .NET host.
//
// Args: --shell <powershell|pwsh|cmd> --cwd <dir> --cols <n> --rows <n>
// Host -> bridge (one JSON object per line):
//   { "type": "input", "data": "..." }        raw keystrokes for the pty
//   { "type": "resize", "cols": n, "rows": n } terminal size
// Bridge -> host (one JSON object per line):
//   { "type": "output", "data": "..." }       raw pty bytes (VT intact)
//   { "type": "exit", "code": n }             shell exited
//   { "type": "error", "message": "..." }     fatal bridge error

const pty = require('node-pty');

function arg(name, fallback) {
  const i = process.argv.indexOf(name);
  return i >= 0 && i + 1 < process.argv.length ? process.argv[i + 1] : fallback;
}

function shellCommand(shell) {
  switch ((shell || '').toLowerCase()) {
    case 'pwsh':
      return { file: 'pwsh.exe', args: ['-NoProfile', '-NoExit'] };
    case 'cmd':
      return { file: 'cmd.exe', args: ['/q'] };
    default:
      return { file: 'powershell.exe', args: ['-NoProfile', '-NoExit'] };
  }
}

function send(obj) {
  process.stdout.write(JSON.stringify(obj) + '\n');
}

function main() {
  const shell = arg('--shell', 'powershell');
  const cwd = arg('--cwd', undefined);
  const cols = Math.min(500, Math.max(20, parseInt(arg('--cols', '120'), 10) || 120));
  const rows = Math.min(200, Math.max(5, parseInt(arg('--rows', '30'), 10) || 30));
  const { file, args } = shellCommand(shell);

  if (!process.env.TERM) {
    process.env.TERM = 'xterm-256color';
  }

  let term;
  try {
    term = pty.spawn(file, args, { name: 'xterm-256color', cols, rows, cwd, useConpty: true });
  } catch (err) {
    send({ type: 'error', message: `Failed to start shell '${file}': ${(err && err.message) || err}` });
    process.exitCode = 1;
    return;
  }

  term.onData((data) => {
    send({ type: 'output', data });
  });

  term.onExit(({ exitCode }) => {
    send({ type: 'exit', code: exitCode });
    setTimeout(() => process.exit(0), 250);
  });

  let buffer = '';
  process.stdin.setEncoding('utf8');
  process.stdin.on('data', (chunk) => {
    buffer += chunk;
    let idx;
    while ((idx = buffer.indexOf('\n')) >= 0) {
      const line = buffer.slice(0, idx).trim();
      buffer = buffer.slice(idx + 1);
      if (!line) continue;

      let msg;
      try {
        msg = JSON.parse(line);
      } catch {
        continue;
      }

      if (msg.type === 'input' && typeof msg.data === 'string' && msg.data.length > 0) {
        term.write(msg.data.slice(0, 65536));
      } else if (msg.type === 'resize' && msg.cols > 0 && msg.rows > 0) {
        try {
          term.resize(Math.min(500, msg.cols), Math.min(200, msg.rows));
        } catch {
          // ignore resize failures on dead shells
        }
      }
    }
  });

  process.stdin.on('end', () => process.exit(0));
  process.stdin.resume();
}

main();
