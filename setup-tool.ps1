$PackageId = "up.DbSql"
$ProjectDir = "SqlWarehouseNet"
$Config = "Release"

Write-Host "📦 Pack automatique du .NET Tool..." -ForegroundColor Cyan

# 1. Nettoyage et Pack
dotnet pack $ProjectDir -c $Config --output ./nupkg

if ($LASTEXITCODE -ne 0) {
    Write-Error "Le pack a échoué."
    exit $LASTEXITCODE
}

Write-Host "🚀 Installation ou Mise à jour locale du tool..." -ForegroundColor Green
dotnet tool update -g $PackageId --add-source ./nupkg

Write-Host "✅ Terminé ! Tu peux maintenant utiliser la commande : dbsql" -ForegroundColor Cyan
