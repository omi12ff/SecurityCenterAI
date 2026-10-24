namespace SecurityCenterAI.Agent;

public static class AgentStorageDirectory
{
    public static string Resolve(string? localApplicationData)
    {
        if (string.IsNullOrWhiteSpace(localApplicationData) || !Path.IsPathFullyQualified(localApplicationData))
            throw new AgentException("Windows no informó una ubicación local de datos válida. No se guardará estado en la carpeta del agente.");
        return Path.Combine(localApplicationData, "SecurityCenterAI", "Agent");
    }
}
