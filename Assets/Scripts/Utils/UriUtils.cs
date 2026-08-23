using System;

namespace XRVLC.Utils
{
    public static class UriUtils
    {
        /// <summary>
        /// 对 URI 进行脱敏处理，隐藏 smb 协议中的明文密码
        /// </summary>
        public static string RedactUri(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return uri;
            try {
                if (uri.StartsWith("smb://")) {
                    var atIndex = uri.IndexOf('@');
                    if (atIndex > 6) {
                        return "smb://***:***" + uri.Substring(atIndex);
                    }
                }
            } catch {}
            return uri;
        }
    }
}