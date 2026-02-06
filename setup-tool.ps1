$PackageId = "up.DbSql"
$ProjectDir = "SqlWarehouseNet"
$Config = "Release"
$NupkgDir = "./nupkg"

Write-Host "🧹 Cleaning previous packages..." -ForegroundColor DarkGray
if (Test-Path $NupkgDir) { Remove-Item "$NupkgDir/*.nupkg" -ErrorAction SilentlyContinue }

Write-Host "📦 Packing the .NET Tool..." -ForegroundColor Cyan
dotnet pack $ProjectDir -c $Config --output $NupkgDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Pack failed."
    exit $LASTEXITCODE
}

$nupkg = Get-ChildItem "$NupkgDir/*.nupkg" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host "📦 Package: $($nupkg.Name)" -ForegroundColor DarkGray

Write-Host "🚀 Installing/updating the tool locally..." -ForegroundColor Green
dotnet tool update -g $PackageId --add-source $NupkgDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "⚠️  Update failed — trying fresh install..." -ForegroundColor Yellow
    dotnet tool uninstall -g $PackageId 2>$null
    dotnet tool install -g $PackageId --add-source $NupkgDir
}

Write-Host "✅ Done! Run the tool with: dbsql" -ForegroundColor Cyan
Write-Host "📌 Version: $((Select-Xml -Path "$ProjectDir/$ProjectDir.csproj" -XPath '//Version').Node.InnerText)" -ForegroundColor DarkGray
