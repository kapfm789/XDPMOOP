# W5-01: độ bao phủ dòng lệnh của ba module lõi của core (NFR-MAINT-02), gộp test đơn vị và test tích hợp.
# Cách chia module và cách chạy: docs/testing/coverage.md.
#
#   pwsh tools/coverage/core-coverage.ps1 -ResultsDir <thư mục chứa các coverage.cobertura.xml> [-Threshold 80] [-Output báo-cáo.md]
#
# Một dòng được tính là đã chạy khi ít nhất một file cobertura ghi nó có lượt chạy. Module nào dưới ngưỡng thì thoát mã 1.
param(
    [Parameter(Mandatory = $true)] [string] $ResultsDir,
    [double] $Threshold = 80,
    [string] $Output
)

$ErrorActionPreference = 'Stop'

# Đường dẫn tính từ services/core/src/. Mỗi file thuộc module đầu tiên khớp, theo thứ tự dưới đây: Costing và Reservation
# trước, để phần còn lại của thư mục Inventory mới tính cho Inventory.
$modules = [ordered]@{
    Costing     = @(
        '^Oism\.Core\.Domain/Inventory/WeightedAverageCost\.cs$',
        '^Oism\.Core\.Application/Inventory/ConfirmPurchaseReceipt/',
        '^Oism\.Core\.Application/Orders/ConfirmOrder/'
    )
    Reservation = @(
        '^Oism\.Core\.Domain/Orders/Reservation\.cs$',
        '^Oism\.Core\.Application/Inventory/IStockService\.cs$',
        '^Oism\.Core\.Application/Orders/ReserveStock/',
        '^Oism\.Core\.Application/Orders/CancelOrder/',
        '^Oism\.Core\.Infrastructure/Jobs/ExpireReservationsJob\.cs$'
    )
    Inventory   = @(
        '^Oism\.Core\.Domain/Inventory/',
        '^Oism\.Core\.Application/Inventory/',
        '^Oism\.Core\.Infrastructure/Repositories/(InventoryRepository|PurchaseReceiptRepository|StockTransferRepository|StocktakeRepository|StockQueries)\.cs$'
    )
}

function Get-Module([string] $path) {
    foreach ($module in $modules.Keys) {
        foreach ($pattern in $modules[$module]) {
            if ($path -match $pattern) { return $module }
        }
    }
    return $null
}

$reports = @(Get-ChildItem -Path $ResultsDir -Recurse -Filter 'coverage.cobertura.xml')
if ($reports.Count -eq 0) { throw "No coverage.cobertura.xml under $ResultsDir" }

# file|dòng -> đã chạy hay chưa, gộp mọi báo cáo.
$lines = @{}
foreach ($report in $reports) {
    [xml] $xml = Get-Content -Raw -Path $report.FullName
    foreach ($class in $xml.coverage.packages.package.classes.class) {
        $file = ($class.filename -replace '\\', '/')
        if ($file -notmatch 'services/core/src/(.+)$') { continue }
        $path = $Matches[1]
        if ($null -eq (Get-Module $path)) { continue }
        foreach ($line in $class.lines.line) {
            $key = "$path|$($line.number)"
            $hit = [int] $line.hits -gt 0
            if (-not $lines.ContainsKey($key) -or $hit) { $lines[$key] = $lines[$key] -or $hit }
        }
    }
}

$rows = foreach ($module in $modules.Keys) {
    $owned = @($lines.Keys | Where-Object { (Get-Module ($_ -split '\|')[0]) -eq $module })
    $covered = @($owned | Where-Object { $lines[$_] }).Count
    $total = $owned.Count
    $files = @($owned | ForEach-Object { ($_ -split '\|')[0] } | Sort-Object -Unique).Count
    [pscustomobject] @{
        Module  = $module
        Files   = $files
        Covered = $covered
        Total   = $total
        Percent = if ($total -eq 0) { 0 } else { [math]::Round(100 * $covered / $total, 1) }
    }
}

$culture = [System.Globalization.CultureInfo]::GetCultureInfo('vi-VN')
$table = @('| Module | Số file | Dòng đã chạy | Tổng dòng | Tỉ lệ |', '| --- | --- | --- | --- | --- |')
$table += $rows | ForEach-Object { "| $($_.Module) | $($_.Files) | $($_.Covered) | $($_.Total) | $($_.Percent.ToString('0.0', $culture))% |" }
$table | ForEach-Object { Write-Output $_ }
if ($Output) { $table | Set-Content -Path $Output -Encoding utf8 }

# Dòng chưa chạy, để biết còn hở ở đâu.
$missed = @($lines.Keys | Where-Object { -not $lines[$_] } | Sort-Object)
if ($missed.Count -gt 0) {
    Write-Output ''
    Write-Output 'Uncovered lines:'
    $missed | ForEach-Object { Write-Output "  $_" }
}

$below = @($rows | Where-Object { $_.Percent -lt $Threshold })
if ($below.Count -gt 0) {
    Write-Error ("Coverage below {0}%: {1}" -f $Threshold, (($below | ForEach-Object { $_.Module }) -join ', '))
    exit 1
}
