namespace SecurityCenterAI.Agent;

// Only fixed, user-facing messages belong here. Never wrap server responses or secrets.
public sealed class AgentException(string message) : Exception(message);
