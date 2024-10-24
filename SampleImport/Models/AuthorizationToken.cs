using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleImport.Models {
    public class AuthorizationToken {
        public string AccessToken { get; set; }
        public string TokenType { get; set; }
        public string ExpiresIn { get; set; }
        public string Status { get; set; }
        public int RetryAttempts { get; set; }
        public int FailedTokenAttempts { get; set; }
        public DateTime LastCheck { get; set; }

        public AuthorizationToken(string access_token, string token_type, string expires_in) {
            this.AccessToken = access_token;
            this.TokenType = token_type;
            this.ExpiresIn = expires_in;
            this.Status = "Active";
            this.RetryAttempts = 0;
            this.FailedTokenAttempts = 0;
        }
    }
}
