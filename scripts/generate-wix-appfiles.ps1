param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir,
    [Parameter(Mandatory = $true)]
    [string]$OutputFile
)

$ErrorActionPreference = "Stop"

$publishPath = [System.IO.Path]::GetFullPath($PublishDir)
$outputPath = [System.IO.Path]::GetFullPath($OutputFile)

New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($outputPath)) | Out-Null

$files = Get-ChildItem -Path $publishPath -File -Recurse | Sort-Object FullName

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <DirectoryRef Id="INSTALLFOLDER">')

$i = 0
foreach ($f in $files) {
    $i++
    $relative = $f.FullName.Substring($publishPath.Length).TrimStart('\')
    $source = ('$(var.PublishDir)\' + $relative).Replace('&', '&amp;').Replace('"', '&quot;').Replace('<', '&lt;').Replace('>', '&gt;')
    [void]$sb.AppendLine(("      <Component Id=""cmp{0}"" Guid=""*"">" -f $i))
    [void]$sb.AppendLine(("        <File Id=""fil{0}"" KeyPath=""yes"" Source=""{1}"" />" -f $i, $source))
    [void]$sb.AppendLine('      </Component>')
}

[void]$sb.AppendLine('    </DirectoryRef>')
[void]$sb.AppendLine('    <ComponentGroup Id="AppFiles">')
for ($n = 1; $n -le $i; $n++) {
    [void]$sb.AppendLine(("      <ComponentRef Id=""cmp{0}"" />" -f $n))
}
[void]$sb.AppendLine('    </ComponentGroup>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('</Wix>')

[System.IO.File]::WriteAllText($outputPath, $sb.ToString(), [System.Text.Encoding]::UTF8)
