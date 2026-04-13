param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [Parameter(Mandatory = $true)]
    [string]$RootKey,

    [Parameter(Mandatory = $true)]
    [string]$FieldName,

    [string]$DefaultValue = '""',

    [string]$EntryIdKey = "id",

    [string]$AnchorField = "id",

    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-IndentCount {
    param([string]$Line)

    $trimmed = $Line.TrimStart(' ')
    return $Line.Length - $trimmed.Length
}

function Get-FieldValue {
    param(
        [string]$Content,
        [string]$Field
    )

    $pattern = "^(?:-\s*)?" + [regex]::Escape($Field) + "\s*:\s*(.*)$"
    $match = [regex]::Match($Content, $pattern)
    if (-not $match.Success) {
        return $null
    }

    return $match.Groups[1].Value.Trim()
}

function Finalize-Entry {
    param(
        [string]$FilePath,
        [System.Collections.Generic.List[string]]$Lines,
        [System.Collections.Generic.List[object]]$MissingEntries,
        [System.Collections.Generic.List[object]]$Insertions,
        [int]$EntryStartIndex,
        [int]$EntryIndent,
        [string]$EntryId,
        [bool]$HasField,
        [int]$AnchorIndex,
        [string]$FieldName,
        [string]$DefaultValue,
        [switch]$ApplyChanges
    )

    if ($EntryStartIndex -lt 0 -or $HasField) {
        return
    }

    $resolvedEntryId = if ([string]::IsNullOrWhiteSpace($EntryId)) { "<unknown>" } else { $EntryId }
    $MissingEntries.Add([pscustomobject]@{
        FilePath = $FilePath
        EntryId = $resolvedEntryId
        EntryLine = $EntryStartIndex + 1
    }) | Out-Null

    if (-not $ApplyChanges) {
        return
    }

    $insertAfterIndex = if ($AnchorIndex -ge 0) { $AnchorIndex } else { $EntryStartIndex }
    $fieldIndent = " " * ($EntryIndent + 2)
    $Insertions.Add([pscustomobject]@{
        Index = $insertAfterIndex + 1
        Text = "$fieldIndent$FieldName`: $DefaultValue"
        EntryId = $resolvedEntryId
    }) | Out-Null
}

if (-not (Test-Path -LiteralPath $Path)) {
    throw "Path not found: $Path"
}

$resolvedPath = (Resolve-Path -LiteralPath $Path).Path
$yamlFiles = Get-ChildItem -LiteralPath $resolvedPath -Recurse -File -Filter *.yaml | Sort-Object FullName

if ($yamlFiles.Count -eq 0) {
    Write-Warning "No .yaml files were found under $resolvedPath"
    exit 0
}

$totalMissing = 0
$totalInserted = 0

Write-Host "Scanning bundle: $resolvedPath"
Write-Host "RootKey=$RootKey FieldName=$FieldName EntryIdKey=$EntryIdKey AnchorField=$AnchorField Apply=$Apply"

foreach ($file in $yamlFiles) {
    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        $lines.Add($line) | Out-Null
    }

    $missingEntries = [System.Collections.Generic.List[object]]::new()
    $insertions = [System.Collections.Generic.List[object]]::new()

    $inRoot = $false
    $rootIndent = -1
    $entryStartIndex = -1
    $entryIndent = -1
    $entryId = ""
    $hasField = $false
    $anchorIndex = -1

    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
            continue
        }

        $indent = Get-IndentCount -Line $line

        if (-not $inRoot) {
            if ($trimmed -eq "${RootKey}:") {
                $inRoot = $true
                $rootIndent = $indent
            }

            continue
        }

        if ($indent -le $rootIndent -and -not $trimmed.StartsWith("- ")) {
            Finalize-Entry -FilePath $file.FullName -Lines $lines -MissingEntries $missingEntries -Insertions $insertions `
                -EntryStartIndex $entryStartIndex -EntryIndent $entryIndent -EntryId $entryId -HasField $hasField `
                -AnchorIndex $anchorIndex -FieldName $FieldName -DefaultValue $DefaultValue -ApplyChanges:$Apply

            $entryStartIndex = -1
            $entryIndent = -1
            $entryId = ""
            $hasField = $false
            $anchorIndex = -1
            $inRoot = $false
            $rootIndent = -1

            if ($trimmed -eq "${RootKey}:") {
                $inRoot = $true
                $rootIndent = $indent
            }

            continue
        }

        if ($trimmed.StartsWith("- ") -and $indent -eq ($rootIndent + 2)) {
            Finalize-Entry -FilePath $file.FullName -Lines $lines -MissingEntries $missingEntries -Insertions $insertions `
                -EntryStartIndex $entryStartIndex -EntryIndent $entryIndent -EntryId $entryId -HasField $hasField `
                -AnchorIndex $anchorIndex -FieldName $FieldName -DefaultValue $DefaultValue -ApplyChanges:$Apply

            $entryStartIndex = $index
            $entryIndent = $indent
            $entryId = ""
            $hasField = $false
            $anchorIndex = -1

            $lineContent = $trimmed.Substring(2).Trim()
            $entryIdValue = Get-FieldValue -Content $lineContent -Field $EntryIdKey
            if ($null -ne $entryIdValue) {
                $entryId = $entryIdValue.Trim('"')
            }

            if ($lineContent -match ("^" + [regex]::Escape($FieldName) + "\s*:")) {
                $hasField = $true
            }

            if ($lineContent -match ("^" + [regex]::Escape($AnchorField) + "\s*:")) {
                $anchorIndex = $index
            }

            continue
        }

        if ($entryStartIndex -lt 0) {
            continue
        }

        if ($indent -le $rootIndent) {
            continue
        }

        $fieldValue = Get-FieldValue -Content $trimmed -Field $FieldName
        if ($null -ne $fieldValue) {
            $hasField = $true
        }

        if ([string]::IsNullOrWhiteSpace($entryId)) {
            $entryIdValue = Get-FieldValue -Content $trimmed -Field $EntryIdKey
            if ($null -ne $entryIdValue) {
                $entryId = $entryIdValue.Trim('"')
            }
        }

        if ($anchorIndex -lt 0) {
            $anchorValue = Get-FieldValue -Content $trimmed -Field $AnchorField
            if ($null -ne $anchorValue) {
                $anchorIndex = $index
            }
        }
    }

    Finalize-Entry -FilePath $file.FullName -Lines $lines -MissingEntries $missingEntries -Insertions $insertions `
        -EntryStartIndex $entryStartIndex -EntryIndent $entryIndent -EntryId $entryId -HasField $hasField `
        -AnchorIndex $anchorIndex -FieldName $FieldName -DefaultValue $DefaultValue -ApplyChanges:$Apply

    $totalMissing += $missingEntries.Count

    if ($missingEntries.Count -eq 0) {
        Write-Host "[OK] $($file.FullName) has no missing '$FieldName' entries under '$RootKey'."
        continue
    }

    Write-Warning "[MISSING] $($file.FullName) has $($missingEntries.Count) entries missing '$FieldName'."
    foreach ($entry in $missingEntries) {
        Write-Host ("  - line {0}: {1}" -f $entry.EntryLine, $entry.EntryId)
    }

    if ($Apply -and $insertions.Count -gt 0) {
        foreach ($insertion in ($insertions | Sort-Object Index -Descending)) {
            $lines.Insert($insertion.Index, $insertion.Text)
            $totalInserted++
        }

        Set-Content -LiteralPath $file.FullName -Value $lines -Encoding UTF8
        Write-Host "[APPLIED] Inserted $($insertions.Count) '$FieldName' lines into $($file.FullName)."
    }
}

Write-Host ""
Write-Host "Summary:"
Write-Host "  YAML files scanned: $($yamlFiles.Count)"
Write-Host "  Missing field entries: $totalMissing"
Write-Host "  Inserted fields: $totalInserted"

if (-not $Apply) {
    Write-Host "Audit mode only. Re-run with -Apply to write default fields into files."
}
