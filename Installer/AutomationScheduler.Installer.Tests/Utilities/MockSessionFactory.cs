using WixToolset.Dtf.WindowsInstaller;
using Moq;
using System.Collections.Generic;

namespace AutomationScheduler.Installer.Tests.Utilities
{
    public static class MockSessionFactory
    {
        public static Mock<Session> CreateMockSession(Dictionary<string, string>? properties = null)
        {
            var mockSession = new Mock<Session>();
            var sessionProperties = properties ?? new Dictionary<string, string>();

            // Setup property access
            mockSession.Setup(s => s[It.IsAny<string>()])
                .Returns<string>(key => (sessionProperties.GetValueOrDefault(key)) ?? string.Empty);

            // Setup property setter
            mockSession.SetupSet(s => s[It.IsAny<string>()] = It.IsAny<string>())
                .Callback<string, string>((key, value) => sessionProperties[key] = value);

            // Setup logging
            var logs = new List<string>();
            mockSession.Setup(s => s.Log(It.IsAny<string>()))
                .Callback<string>(message => logs.Add(message));

            // Add a way to retrieve logs for verification
            _ = mockSession.Setup(s => s.Database).Returns((Database)null);
            
            return mockSession;
        }

        public static Mock<Session> CreateMockSessionWithInstallFolder(string installFolder)
        {
            return CreateMockSession(new Dictionary<string, string>
            {
                ["INSTALLFOLDER"] = installFolder
            });
        }

        public static Mock<Session> CreateMockSessionForAzureDevOps(
            string installFolder,
            string configuration = "Default",
            string azureDevOpsUrl = "https://dev.azure.com/testorg",
            string pat = "test-pat",
            string feed = "TestFeed")
        {
            return CreateMockSession(new Dictionary<string, string>
            {
                ["INSTALLFOLDER"] = installFolder,
                ["SELECTEDCONFIGURATION"] = configuration,
                ["AZUREDEVOPS_URL"] = azureDevOpsUrl,
                ["AZUREDEVOPS_PAT"] = pat,
                ["AZUREDEVOPS_FEED"] = feed
            });
        }
    }
}