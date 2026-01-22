using Microsoft.Extensions.AI;

namespace Wingman.Agent.Tools;

public static class ToolsFactory
{
    public static IReadOnlyList<AITool> CreateDefaultTools()
    {
        return CreateFileOrganizerTools();
    }

    public static IReadOnlyList<AITool> CreateFileOrganizerTools()
    {
        return
        [
            AIFunctionFactory.Create(FileOrganizerTools.FindDirectory),
            AIFunctionFactory.Create(FileOrganizerTools.ListDirectory),
            AIFunctionFactory.Create(FileOrganizerTools.SearchFiles),
            
            AIFunctionFactory.Create(FileOrganizerTools.MoveItem),
            AIFunctionFactory.Create(FileOrganizerTools.CopyFile),
            AIFunctionFactory.Create(FileOrganizerTools.CreateDirectory),
            AIFunctionFactory.Create(FileOrganizerTools.DeleteItem),
            
            AIFunctionFactory.Create(FileOrganizerTools.PreviewOrganizeByExtension),
            AIFunctionFactory.Create(FileOrganizerTools.ApplyOrganizationPlan),
        ];
    }
}
