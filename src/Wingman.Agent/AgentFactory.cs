using System.Runtime.Versioning;
using Wingman.Agent.Agents;
using Wingman.Agent.Configuration;

namespace Wingman.Agent;

[SupportedOSPlatform("windows")]
public static class AgentFactory
{
    public static IWingmanAgent Create(AgentType type, WingmanConfig config)
    {
        return type switch
        {
            AgentType.FileOrganizer => new FileOrganizerAgent(config),
            AgentType.Spreadsheet => new SpreadsheetAgent(config),
            AgentType.SpreadsheetCreator => new SpreadsheetCreatorAgent(config),
            AgentType.Pdf => new PdfAgent(config),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown agent type")
        };
    }

    public static FileOrganizerAgent CreateFileOrganizerAgent(WingmanConfig config) => new(config);

    public static SpreadsheetAgent CreateSpreadsheetAgent(WingmanConfig config) => new(config);

    public static SpreadsheetCreatorAgent CreateSpreadsheetCreatorAgent(WingmanConfig config) => new(config);

    public static PdfAgent CreatePdfAgent(WingmanConfig config) => new(config);

    public static IReadOnlyList<AgentInfo> GetAvailableAgents()
    {
        return
        [
            new AgentInfo(AgentType.FileOrganizer, "FileOrganizer", 
                "Specializes in organizing, moving, copying, and managing files and directories."),
            new AgentInfo(AgentType.Spreadsheet, "Spreadsheet", 
                "Specializes in reading, analyzing, and querying Excel and CSV files."),
            new AgentInfo(AgentType.SpreadsheetCreator, "SpreadsheetCreator", 
                "Specializes in creating Excel spreadsheets with structured data following best practices."),
            new AgentInfo(AgentType.Pdf, "PDF", 
                "Specializes in reading and extracting content from PDF documents."),
        ];
    }

    public enum AgentType
    {
        FileOrganizer,
        Spreadsheet,
        SpreadsheetCreator,
        Pdf
    }
}

public sealed record AgentInfo(AgentFactory.AgentType Type, string Name, string Description);
