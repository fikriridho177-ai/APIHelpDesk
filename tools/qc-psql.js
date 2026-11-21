const { spawnSync } = require('node:child_process')
const path = require('node:path')
const cfg = require(path.join(__dirname, '..', 'src', 'AuthUtility.Api', 'appsettings.json'))
const parts = {}
for (const kv of cfg.ConnectionStrings.DefaultConnection.split(';')) {
  const i = kv.indexOf('=')
  if (i > 0) parts[kv.slice(0, i).trim().toLowerCase()] = kv.slice(i + 1).trim()
}
const args = process.argv.slice(2)
let db = parts['database'] || parts['initial catalog']
if (args[0] === '--db') { db = args[1]; args.splice(0, 2) }
const r = spawnSync('C:/Program Files/PostgreSQL/18/bin/psql.exe', [
  '-h', parts['host'] || parts['server'] || 'localhost',
  '-p', parts['port'] || '5432',
  '-U', parts['username'] || parts['user id'],
  '-d', db, '-v', 'ON_ERROR_STOP=1', ...args,
], { env: { ...process.env, PGPASSWORD: parts['password'] || '' }, stdio: 'inherit' })
process.exit(r.status ?? 1)