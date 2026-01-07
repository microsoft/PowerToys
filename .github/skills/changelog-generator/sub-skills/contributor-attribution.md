# Contributor Attribution Rules

This sub-skill defines when and how to credit contributors in changelog entries.

## Attribution Rules

- Check [COMMUNITY.md](../../../COMMUNITY.md) for the "PowerToys core team" section
- If author is **NOT** in core team → Add `Thanks [@username](https://github.com/username)!`
- If author **IS** in core team → No attribution needed
- Microsoft employees working on PowerToys as their job → No attribution needed

## Core Team Members

Current core team members (from COMMUNITY.md) - do NOT thank these:

```
@craigloewen-msft, @niels9001, @dhowett, @yeelam-gordon, @jamrobot, 
@lei9444, @shuaiyuanxx, @moooyo, @haoliuu, @chenmy77, @chemwolf6922,
@yaqingmi, @zhaoqpcn, @urnotdfs, @zhaopy536, @wang563681252, @vanzue,
@zadjii-msft, @khmyznikov, @chatasweetie, @MichaelJolley, @Jaylyn-Barbee,
@zateutsch, @crutkas
```

## Check Author Script

```powershell
$coreTeam = @(
    'craigloewen-msft', 'niels9001', 'dhowett', 'yeelam-gordon', 'jamrobot',
    'lei9444', 'shuaiyuanxx', 'moooyo', 'haoliuu', 'chenmy77', 'chemwolf6922',
    'yaqingmi', 'zhaoqpcn', 'urnotdfs', 'zhaopy536', 'wang563681252', 'vanzue',
    'zadjii-msft', 'khmyznikov', 'chatasweetie', 'MichaelJolley', 'Jaylyn-Barbee',
    'zateutsch', 'crutkas'
)

function Get-Attribution {
    param([string]$author)
    
    if ($coreTeam -contains $author) {
        return $null  # No attribution needed
    }
    return "Thanks [@$author](https://github.com/$author)!"
}

# Usage
$author = gh pr view 12345 --repo microsoft/PowerToys --json author --jq '.author.login'
$attribution = Get-Attribution $author
if ($attribution) {
    Write-Host "Add: $attribution"
} else {
    Write-Host "No attribution needed (core team member)"
}
```

## Attribution Format

**With attribution:**
```markdown
 - The Awake countdown timer now stays accurate over long periods. Thanks [@daverayment](https://github.com/daverayment)!
```

**Without attribution (core team):**
```markdown
 - Added new feature to Command Palette for opening settings.
```

## Special Cases

1. **Co-authored commits**: Credit the primary author (first in list)
2. **Bot accounts** (dependabot, etc.): No attribution
3. **Former core team members**: Check if they were core team at time of PR
4. **Multiple PRs by same external contributor**: Thank them on each entry

## High-Impact Community Members

These contributors have made significant ongoing contributions and are recognized in COMMUNITY.md.
**ALWAYS thank these contributors** - they are NOT core team and deserve recognition:

```
@davidegiacometti, @htcfreek, @daverayment, @jiripolasek
```

Check COMMUNITY.md for the full up-to-date list under "High impact community members" section.

## Updated Check Author Script

```powershell
$coreTeam = @(
    'craigloewen-msft', 'niels9001', 'dhowett', 'yeelam-gordon', 'jamrobot',
    'lei9444', 'shuaiyuanxx', 'moooyo', 'haoliuu', 'chenmy77', 'chemwolf6922',
    'yaqingmi', 'zhaoqpcn', 'urnotdfs', 'zhaopy536', 'wang563681252', 'vanzue',
    'zadjii-msft', 'khmyznikov', 'chatasweetie', 'MichaelJolley', 'Jaylyn-Barbee',
    'zateutsch', 'crutkas'
)

# High-impact community members - ALWAYS thank these!
$highImpactCommunity = @(
    'davidegiacometti', 'htcfreek', 'daverayment', 'jiripolasek'
)

function Get-Attribution {
    param([string]$author)
    
    # Core team and bots don't need thanks
    if ($coreTeam -contains $author -or $author -match '\[bot\]$') {
        return $null
    }
    # Everyone else (including high-impact community) gets thanked
    return "Thanks [@$author](https://github.com/$author)!"
}
```
