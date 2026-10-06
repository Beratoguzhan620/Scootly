[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Invoke-K6 {
    param([string]$Script, [string]$Name, [string[]]$EnvArgs,
          [string]$Show = 'telemetry_|rides_|race_|http_req_failed|http_req_duration\.\.|iterations\.|dropped')
    $root = Join-Path $PWD 'tests\Scootly.LoadTests'
    New-Item -ItemType Directory -Force (Join-Path $root 'results') | Out-Null
    $cname = "k6-$Name"
    docker rm -f $cname *> $null

    $a = @('run', '-d', '--name', $cname)
    foreach ($e in $EnvArgs) { $a += @('-e', $e) }
    $a += @('-v', "${root}:/scripts", 'grafana/k6:latest', 'run', '--summary-export', "/scripts/results/$Name.json", "/scripts/$Script")
    docker @a | Out-Null

    $peak = @{}
    while (docker ps -q -f "name=^/$cname$") {
        docker stats --no-stream --format '{{.Name}}|{{.CPUPerc}}|{{.MemUsage}}' $cname deploy-api-1 deploy-worker-1 scootly-postgres scootly-rabbitmq scootly-redis | ForEach-Object {
            $p = $_ -split '\|'
            $cpu = [double]($p[1].TrimEnd('%'))
            if (-not $peak.ContainsKey($p[0]) -or $cpu -gt $peak[$p[0]].Cpu) {
                $peak[$p[0]] = [pscustomobject]@{ Cpu = $cpu; Mem = ($p[2] -split ' / ')[0] }
            }
        }
        Start-Sleep -Seconds 3
    }

    $log = @(docker logs $cname 2>&1 | ForEach-Object { "$_" })
    docker rm $cname | Out-Null
    $log | Select-String -Pattern $Show | ForEach-Object { $_.Line.Trim() }
    $peak.GetEnumerator() | Sort-Object Name | ForEach-Object { "{0,-28} cpu={1,6}%  bellek={2}" -f $_.Name, $_.Value.Cpu, $_.Value.Mem }
}

$base = 'BASE_URL=http://host.docker.internal:5016'
$countSql = 'SELECT COUNT(*) FROM "TelemetryReadings";'
function Get-Rows { [long](($countSql | docker exec -i scootly-postgres psql -U postgres -d scootly -t -A).Trim()) }