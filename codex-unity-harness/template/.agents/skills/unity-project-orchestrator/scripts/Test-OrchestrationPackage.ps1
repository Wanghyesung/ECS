param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'
$resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
$workItemDirectory = Join-Path $resolvedPackage 'work-items'
$handoffDirectory = Join-Path $resolvedPackage 'ready-for-integration'

if ((Test-Path -LiteralPath (Join-Path $resolvedPackage 'project.md')) -eq $false) {
    throw 'project.md가 없습니다.'
}

if ((Test-Path -LiteralPath $workItemDirectory) -eq $false) {
    throw 'work-items 디렉터리가 없습니다.'
}

$requiredHeadings = @(
    '## Scope',
    '## Ownership',
    '## Contracts',
    '## Serialization',
    '## Unity wiring',
    '## Verification',
    '## Handoff'
)

$ownedPathOwners = @{}
$workItems = Get-ChildItem -LiteralPath $workItemDirectory -Filter '*.md' -File

if ($workItems.Count -eq 0) {
    throw '검증할 작업 항목이 없습니다.'
}

foreach ($workItem in $workItems) {
    $content = Get-Content -Raw -LiteralPath $workItem.FullName

    if ($content -notmatch '(?m)^status:\s*(planned|in-progress|ready-for-integration|blocked|done|example)\s*$') {
        throw "$($workItem.Name): 유효한 status가 없습니다."
    }

    foreach ($heading in $requiredHeadings) {
        if ($content.Contains($heading) -eq $false) {
            throw "$($workItem.Name): '$heading' 섹션이 없습니다."
        }
    }

    $ownedBlock = [regex]::Match(
        $content,
        '(?m)^owned_paths:[ \t]*\r?\n(?<items>(?:[ \t]{2}-[ \t]+[^\r\n]+\r?\n?)+)'
    )

    if ($ownedBlock.Success -eq $false) {
        throw "$($workItem.Name): owned_paths가 없습니다."
    }

    foreach ($line in ($ownedBlock.Groups['items'].Value -split "`r?`n")) {
        if ($line -notmatch '^\s{2}-\s+(.+?)\s*$') {
            continue
        }

        $ownedPath = $Matches[1]
        if ($ownedPathOwners.ContainsKey($ownedPath)) {
            throw "소유 경로 중복: '$ownedPath' ($($ownedPathOwners[$ownedPath]), $($workItem.Name))"
        }

        $ownedPathOwners[$ownedPath] = $workItem.Name
    }
}

$handoffCount = 0
if (Test-Path -LiteralPath $handoffDirectory) {
    $requiredHandoffHeadings = @(
        '## Changed files',
        '## Implemented contracts',
        '## Serialization',
        '## Unity wiring',
        '## MCP actions',
        '## Verification results',
        '## Remaining risks'
    )

    foreach ($workItem in $workItems) {
        $handoffPath = Join-Path $handoffDirectory $workItem.Name
        if ((Test-Path -LiteralPath $handoffPath) -eq $false) {
            throw "$($workItem.Name): 대응하는 ready-for-integration 인계서가 없습니다."
        }

        $handoffContent = Get-Content -Raw -LiteralPath $handoffPath
        foreach ($heading in $requiredHandoffHeadings) {
            if ($handoffContent.Contains($heading) -eq $false) {
                throw "$($workItem.Name): 인계서에 '$heading' 섹션이 없습니다."
            }
        }

        $handoffCount++
    }
}

Write-Output "PASS: $($workItems.Count)개 작업 항목, $($ownedPathOwners.Count)개 고유 소유 경로, ${handoffCount}개 인계서"
