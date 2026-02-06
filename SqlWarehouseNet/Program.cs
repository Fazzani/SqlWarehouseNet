using SqlWarehouseNet;
using SqlWarehouseNet.Services;

// Bootstrap services and run the application
var profileService = new ProfileService();
using var databricksService = new DatabricksService();
var exportService = new ExportService();

using var app = new App(profileService, databricksService, exportService);
return await app.RunAsync();
