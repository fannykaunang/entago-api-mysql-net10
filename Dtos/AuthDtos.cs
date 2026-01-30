namespace entago_api_mysql.Dtos;

public sealed record ChangePasswordRequest(string OldPassword, string NewPassword);