namespace WhoAndWhat.Application.DTOs.Contacts;

public class ContactOperationResult
{
    public bool Success { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public ContactDto? Contact { get; set; }
}

public class InviteContactResult
{
    public bool Success { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public ContactDto? Contact { get; set; }
    
    public string? InviteCode { get; set; }
    
    public bool InvitationSent { get; set; }
}

public class QRContactResult
{
    public bool Success { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public ContactDto? Contact { get; set; }
    
    public string? QRCodeGenerated { get; set; }
}