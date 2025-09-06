using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("WhoAndWhat.Application.Tests")]
[assembly: InternalsVisibleTo("WhoAndWhat.Domain.Tests")]

namespace WhoAndWhat.Domain;

/// <summary>
/// Assembly reference for the Domain layer.
/// Used for architecture testing and assembly scanning.
/// </summary>
public static class AssemblyReference
{
}