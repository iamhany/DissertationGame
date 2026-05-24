using System;

[Serializable]
public class InterpretationState
{
    public int faithVsSkepticism;    // positive = faith leaning
    public int literalVsSymbolic;    // positive = literal reading
    public int trustInAuthority;     // positive = trusts institutions
}
