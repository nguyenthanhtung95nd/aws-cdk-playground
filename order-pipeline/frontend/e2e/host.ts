import { execFileSync, spawn, type ChildProcess } from 'node:child_process';
import { setTimeout as sleep } from 'node:timers/promises';

// Playwright's own webServer cannot be used for the host on Windows: it always SIGKILLs the
// process group, which takes Aspire's orchestrator down before it can remove its containers.
// So the host is started here and stopped by the returned teardown, which also removes any
// container whose Aspire creator process is gone.

const webUrl = 'http://localhost:5173';
const appHostProject = '../src/OrderPipeline.AppHost/OrderPipeline.AppHost.csproj';
const startupBudgetMs = 5 * 60 * 1000;
const pollIntervalMs = 2000;
const dcpLabel = 'com.microsoft.developer.usvc-dev';

export default async function startHost(): Promise<() => Promise<void>> {
  removeOrphanedAspireContainers();

  if (await isUp(webUrl)) {
    return async () => {};
  }

  const host = spawn('dotnet', ['run', '--project', appHostProject, '--launch-profile', 'e2e'], {
    stdio: ['ignore', 'ignore', 'pipe'],
    windowsHide: true,
  });

  let stderr = '';
  host.stderr.on('data', (chunk: Buffer) => {
    stderr = (stderr + chunk.toString()).slice(-4000);
  });

  await waitUntilUp(host, () => stderr);

  return async () => {
    killTree(host);
    await sleep(pollIntervalMs);
    removeOrphanedAspireContainers();
  };
}

async function waitUntilUp(host: ChildProcess, stderr: () => string): Promise<void> {
  const deadline = Date.now() + startupBudgetMs;

  while (Date.now() < deadline) {
    if (host.exitCode !== null) {
      throw new Error(`The AppHost exited with code ${String(host.exitCode)} before ${webUrl} answered.\n${stderr()}`);
    }

    if (await isUp(webUrl)) {
      return;
    }

    await sleep(pollIntervalMs);
  }

  killTree(host);
  throw new Error(`${webUrl} did not answer within ${String(startupBudgetMs / 1000)}s.\n${stderr()}`);
}

async function isUp(url: string): Promise<boolean> {
  try {
    const response = await fetch(url, { signal: AbortSignal.timeout(pollIntervalMs) });
    return response.ok;
  } catch {
    return false;
  }
}

function killTree(host: ChildProcess): void {
  if (host.exitCode !== null || host.pid === undefined) {
    return;
  }

  if (process.platform === 'win32') {
    execFileSync('taskkill', ['/PID', String(host.pid), '/T', '/F'], { stdio: 'ignore' });
    return;
  }

  host.kill('SIGTERM');
}

function removeOrphanedAspireContainers(): void {
  const listing = execFileSync(
    'docker',
    ['ps', '--filter', `label=${dcpLabel}.persistent=false`, '--format', `{{.ID}} {{.Label "${dcpLabel}.creatorProcessId"}}`],
    { encoding: 'utf8' },
  );

  const orphans = listing
    .split('\n')
    .map((line) => line.trim().split(' '))
    .filter(([id, creator]) => id !== '' && creator !== undefined && !isAlive(Number(creator)))
    .map(([id]) => id);

  if (orphans.length > 0) {
    execFileSync('docker', ['rm', '-f', ...orphans], { stdio: 'ignore' });
  }
}

function isAlive(pid: number): boolean {
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}
