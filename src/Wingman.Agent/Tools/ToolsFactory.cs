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
            AIFunctionFactory.Create(FileOrganizerTools.ListDirectory),
            AIFunctionFactory.Create(FileOrganizerTools.PreviewOrganizeByExtension),
            AIFunctionFactory.Create(FileOrganizerTools.ApplyOrganizationPlan),
        ];
    }
}
