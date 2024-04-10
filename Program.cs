using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;

namespace MyDriveProject
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Check for folder ID argument
            if (args.Length != 4)
            {
                Console.WriteLine("Usage: dotnet run <label-id> <field-id> <selection-id> <folder-id>");
                return;
            }

            string labelId = args[0];
            string fieldId = args[1];
            string selectionId = args[2];
            string folderId = args[3];            

            // Load credentials
            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromFile("./credentials.json").Secrets,
                [DriveService.Scope.Drive],
                "user",
                CancellationToken.None
            );

            // Create Drive service
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "My Drive Project",
            });

            var allFiles = ListFilesRecursively(service, folderId, labelId); // Start with given folderId and labelId         
            Console.WriteLine("allFiles.Count=" + allFiles.Count);
            var numProcessed = 0;

            // 50,000 requests per project per day, which can be increased. 10 queries per second (QPS) per IP address.
            foreach (var file in allFiles)
            {
                numProcessed++;
                Console.WriteLine("File Name: " + file.Name);
                Console.WriteLine("File ID: " + file.Id);
                Console.WriteLine("----------------------");

                // 套用既有標籤到檔案
                // Create the ModifyLabelsRequest object
                var modifyRequest = new ModifyLabelsRequest();

                // Apply label See https://stackoverflow.com/questions/76426947/setting-new-drive-labels-with-selections-to-a-specific-file-using-google-apps-sc
                var addModification = new LabelModification
                {
                    LabelId = labelId,
                    FieldModifications = [
                        new LabelFieldModification
                        {
                            FieldId = fieldId,
                            SetSelectionValues = [selectionId] // 值必須是 selectionId 請先從 label.get API +  得到
                        }
                    ]
                };
                modifyRequest.LabelModifications = [addModification];

                try
                {
                    // Call the ModifyLabels method
                    await service.Files.ModifyLabels(modifyRequest, file.Id).ExecuteAsync();

                    Console.WriteLine("Labels updated successfully! " + (numProcessed * 100 / allFiles.Count) + "% done");

                    System.Threading.Thread.Sleep(250);
                }
                catch (System.Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        static List<Google.Apis.Drive.v3.Data.File> ListFilesRecursively(DriveService service, string parentFolderId, string labelId)
        {
            var results = new List<Google.Apis.Drive.v3.Data.File>();
            var request = service.Files.List();
            request.Q = $"'{parentFolderId}' in parents";  // Modify the 'q' query if needed
            request.Fields = "nextPageToken, files(id, name, mimeType)"; // 需要加入查詢mimeType 才能用做判斷是否為目錄
            request.IncludeItemsFromAllDrives = true; // Include files from Shared Drives
            request.SupportsAllDrives = true;         // Required if including files from Shared Drives             

            do
            {
                var fileList = request.Execute();

                foreach (var file in fileList.Files)
                {
                    if (file.MimeType == "application/vnd.google-apps.folder")
                    {
                        // Folders
                        results.AddRange(ListFilesRecursively(service, file.Id, labelId));
                    }
                    else
                    {
                        // Files

                        // try
                        // {
                        //     var listRequest = service.Files.ListLabels(file.Id);
                        //     var labels = await listRequest.ExecuteAsync();

                        //     foreach (var label in labels.Labels)
                        //     {

                        //         if (label.Id == labelId){
                        //             Console.WriteLine("File Name: "+file.Name+" already labeled.");
                        //             // Already labeled
                        //             continue;
                        //         }
                        //     }                            
                        // }
                        // catch (Exception ex)
                        // {
                        //     Console.WriteLine($"Error getting labels: {ex.Message}");
                        // }

                        // Not labeled yet
                        results.Add(file);
                    }
                }

                request.PageToken = fileList.NextPageToken;
            } while (!String.IsNullOrEmpty(request.PageToken));

            return results;
        }
    }
}
