using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarRental.Application.DTOs
{
    public class MailData
    {
        public string RecieverMail { get; set; }
        public string EmailSubject { get; set; }
        public string EmailBody { get; set; }
    }
}
