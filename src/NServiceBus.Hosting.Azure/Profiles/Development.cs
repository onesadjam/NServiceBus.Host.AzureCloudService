namespace NServiceBus
{
    /// <summary>
    /// Indicates that the infrastructure should configure itself for development.
    /// </summary>
    [ObsoleteEx(ReplacementTypeOrMember = nameof(IConfigureThisEndpoint), RemoveInVersion = "8", TreatAsErrorFromVersion = "7")]
    public class Development : IProfile
    {
    }
}