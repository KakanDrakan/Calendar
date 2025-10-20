using Azure.Core;

namespace CalendarApi.Helpers
{
    public class DelegateCredential : TokenCredential
    {
        private readonly Func<TokenRequestContext, CancellationToken, ValueTask<AccessToken>> _tokenCallback;
        public DelegateCredential(Func<TokenRequestContext, CancellationToken, ValueTask<AccessToken>> tokenCallback)
        {
            _tokenCallback = tokenCallback ?? throw new ArgumentNullException(nameof(tokenCallback));
        }
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return _tokenCallback(requestContext, cancellationToken);
        }
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return _tokenCallback(requestContext, cancellationToken).Result;
        }
    }
    
    
}
