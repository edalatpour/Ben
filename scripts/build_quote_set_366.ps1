$benPath = "c:\Users\tedalatpour\Projects\Ben\Ben.Client\Resources\Quotes\benjamin_franklin.csv"
$witPath = "c:\Users\tedalatpour\Projects\Ben\Ben.Client\Resources\Quotes\wit_and_wisdoms.csv"
$outPath = "c:\Users\tedalatpour\Projects\Ben\Ben.Client\Resources\Quotes\benjamin_franklin_366.csv"

function Normalize-Quote([string]$q) {
    if ([string]::IsNullOrWhiteSpace($q)) { return "" }
    $n = $q.ToLowerInvariant()
    $n = $n -replace '["'']', ''
    $n = $n -replace "[^a-z0-9]", ""
    return $n
}

function Escape-Csv([string]$q) {
    $safe = $q -replace '"', '""'
    return '"' + $safe + '"'
}

$benRows = Import-Csv -Path $benPath
$witRows = Import-Csv -Path $witPath

$all = New-Object System.Collections.Generic.List[object]
foreach ($r in $benRows) {
    $all.Add([pscustomobject]@{ Quote = [string]$r.quote; Source = "ben"; Date = "" })
}
foreach ($r in $witRows) {
    $all.Add([pscustomobject]@{ Quote = [string]$r.Quote; Source = "wit"; Date = [string]$r.Date })
}

$excludePattern = "(?i)\b(arse|shites|wench|maidenhead|harlot|cuckold|cuckolds|blind man's wife|paints her face)\b"

$byNorm = @{}
foreach ($item in $all) {
    $q = ($item.Quote).Trim()
    if ([string]::IsNullOrWhiteSpace($q)) { continue }
    if ($q -match $excludePattern) { continue }

    $norm = Normalize-Quote $q
    if ([string]::IsNullOrWhiteSpace($norm)) { continue }

    $score = 0
    if ($item.Source -eq "ben") { $score += 4 } else { $score += 2 }

    if ($q -match "(?i)\b(new year|christmas|holiday|liberty|freedom|country|nation|independence|taxes|time|year|spring|summer|winter|autumn|fall|harvest|bed|rise|morning|friend|love|virtue|goodness|peace|war)\b") { $score += 4 }
    if ($q -match "(?i)\b(healthy|wealthy|wise|diligence|industry|patience|conscience|gratitude|forgive|forgiveness|kindness)\b") { $score += 2 }
    if ($q.Length -ge 35 -and $q.Length -le 170) { $score += 2 }

    if (-not $byNorm.ContainsKey($norm) -or $score -gt $byNorm[$norm].Score) {
        $byNorm[$norm] = [pscustomobject]@{
            Quote  = $q
            Score  = $score
            Source = $item.Source
            Date   = $item.Date
        }
    }
}

$mustInclude = @(
    "Be at war with your vices, at peace with your neighbors, and let every new year find you a better man.",
    "A good conscience is a continual Christmas.",
    "They who can give up essential liberty to obtain a little temporary safety deserve neither liberty nor safety.",
    "Where liberty is, there is my country.",
    "God grant that not only the love of liberty but a thorough knowledge of the rights of man may pervade all the nations of the earth, so that a philosopher may set his foot anywhere on its surface and say: 'This is my country.'",
    "In this world nothing can be said to be certain, except death and taxes.",
    "Early to bed and early to rise makes a man healthy, wealthy and wise.",
    "Dost thou love life? Then do not squander time, for that is the stuff life is made of.",
    "Lost time is never found again.",
    "Never leave that till tomorrow which you can do today.",
    "One today is worth two tomorrows.",
    "A penny saved is a penny earned.",
    "Pay what you owe, and you'll know what's your own.",
    "Content makes poor men rich; discontent makes rich men poor.",
    "Write injuries in dust, benefits in marble.",
    "A false friend and a shadow attend only while the sun shines.",
    "There was never a good war, or a bad peace.",
    "All wars are follies, very expensive and very mischievous ones.",
    "If we do not hang together, we shall surely hang separately.",
    "Guests, like fish, begin to smell after three days.",
    "There are three faithful friends - an old wife, an old dog, and ready money.",
    "The doors of wisdom are never shut."
)

$selected = New-Object System.Collections.Generic.List[string]
$selectedNorm = @{}

foreach ($m in $mustInclude) {
    $nm = Normalize-Quote $m
    if ($byNorm.ContainsKey($nm) -and -not $selectedNorm.ContainsKey($nm)) {
        $selected.Add($byNorm[$nm].Quote)
        $selectedNorm[$nm] = $true
    }
}

$pool = $byNorm.GetEnumerator() |
ForEach-Object { $_.Value } |
Sort-Object -Property @{Expression = "Score"; Descending = $true }, @{Expression = { $_.Quote.Length }; Descending = $false }, @{Expression = "Quote"; Descending = $false }

foreach ($p in $pool) {
    if ($selected.Count -ge 366) { break }
    $np = Normalize-Quote $p.Quote
    if ($selectedNorm.ContainsKey($np)) { continue }
    $selected.Add($p.Quote)
    $selectedNorm[$np] = $true
}

if ($selected.Count -lt 366) {
    throw "Only selected $($selected.Count) unique quotes; expected at least 366."
}

$selected = $selected | Select-Object -First 366

$outLines = New-Object System.Collections.Generic.List[string]
$outLines.Add('"quote"')
foreach ($q in $selected) {
    $outLines.Add((Escape-Csv $q))
}
Set-Content -Path $outPath -Value $outLines -Encoding UTF8

Write-Output "Created: $outPath"
Write-Output "QuoteCount: $($selected.Count)"
Write-Output "LineCount: $((Get-Content $outPath).Count)"