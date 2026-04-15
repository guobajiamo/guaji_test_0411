param(
    [string[]]$Roots = @("Scripts", "Configs", "Resources", "user_define_file"),
    [switch]$IncludeDocs,
    [switch]$ReportUtf8NoBomChinese
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$textExtensions = @(
    ".cs", ".yaml", ".yml", ".md", ".txt", ".json", ".cfg", ".tres", ".tscn"
)

if (-not $IncludeDocs) {
    $textExtensions = $textExtensions | Where-Object { $_ -notin @(".md", ".txt") }
}

$excludePathRegex = "\\.godot\\|\\android\\build\\|\\Build\\|\\Logs\\|\\bin\\|\\obj\\|\\.git\\|\\.gradle\\"
$mojibakeRegex = "鏆傛|褰撳|鏈€|鎴樻枟|鎴愬氨|鏁欏|澶囨敞|锛|銆\?|鈥|鈽|�"
$utf8Strict = New-Object System.Text.UTF8Encoding($false, $true)

$invalidUtf8Files = New-Object System.Collections.Generic.List[string]
$mojibakeHits = New-Object System.Collections.Generic.List[object]
$utf8NoBomChineseFiles = New-Object System.Collections.Generic.List[string]

foreach ($root in $Roots) {
    if (-not (Test-Path -LiteralPath $root)) {
        continue
    }

    Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object {
        $path = $_.FullName
        if ($path -match $excludePathRegex) {
            return
        }

        if ($_.Extension -notin $textExtensions) {
            return
        }

        # 1) Strict UTF-8 validation
        $bytes = $null
        try {
            $bytes = [System.IO.File]::ReadAllBytes($path)
            [void]$utf8Strict.GetString($bytes)
        }
        catch {
            $invalidUtf8Files.Add($path)
            return
        }

        # Optional report: UTF-8 without BOM + Chinese (easy to look garbled in legacy PS default read)
        if ($ReportUtf8NoBomChinese) {
            $hasUtf8Bom = $bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191
            if (-not $hasUtf8Bom) {
                $text = [System.Text.Encoding]::UTF8.GetString($bytes)
                if ($text -match "[\u4e00-\u9fff]") {
                    $utf8NoBomChineseFiles.Add($path)
                }
            }
        }

        # 2) Suspicious mojibake fragment scan
        $lineNumber = 0
        Get-Content -LiteralPath $path -Encoding UTF8 | ForEach-Object {
            $lineNumber++
            if ($_ -match $mojibakeRegex) {
                $mojibakeHits.Add([pscustomobject]@{
                    File = $path
                    Line = $lineNumber
                    Text = $_.Trim()
                })
            }
        }
    }
}

if ($invalidUtf8Files.Count -gt 0) {
    Write-Host "Found non-UTF8 text files:" -ForegroundColor Red
    $invalidUtf8Files | Sort-Object | ForEach-Object { Write-Host "  $_" }
}

if ($mojibakeHits.Count -gt 0) {
    Write-Host "Found suspicious mojibake fragments:" -ForegroundColor Yellow
    $mojibakeHits |
        Sort-Object File, Line |
        ForEach-Object { Write-Host ("  {0}:{1}  {2}" -f $_.File, $_.Line, $_.Text) }
}

if ($ReportUtf8NoBomChinese -and $utf8NoBomChineseFiles.Count -gt 0) {
    Write-Host "UTF-8(no BOM) Chinese text files (may look garbled in legacy PowerShell default reading):" -ForegroundColor Cyan
    $utf8NoBomChineseFiles | Sort-Object | ForEach-Object { Write-Host "  $_" }
    Write-Host ("Total UTF-8(no BOM) Chinese files: {0}" -f $utf8NoBomChineseFiles.Count) -ForegroundColor Cyan
}

if ($invalidUtf8Files.Count -eq 0 -and $mojibakeHits.Count -eq 0) {
    Write-Host "Check passed: no non-UTF8 files or suspicious mojibake fragments found." -ForegroundColor Green
    exit 0
}

exit 1