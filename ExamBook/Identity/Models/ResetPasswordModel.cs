﻿namespace ExamBook.Identity.Models
{
	public class ResetPasswordModel
	{
		public string UserId { get; set; } = "";
		public string Code { get; set; } = "";
		public string Password { get; set; } = "";
	}
}