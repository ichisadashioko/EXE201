namespace Shioko
{
    public static class Utils
    {
        public static bool ShouldServeIndexHtmlContent(PathString request_path)
        {
            if (request_path == "/")
            {
                return true;
            }

            if (request_path.StartsWithSegments("/api"))
            {
                return false;
            }
            if (request_path.StartsWithSegments("/index.html"))
            {
                return false;
            }

            if (Path.HasExtension(request_path))
            {
                return false;
            }

            if (request_path.StartsWithSegments("/swagger"))
            {
                return false;
            }
            if (request_path.StartsWithSegments("/assets"))
            {
                return false;
            }
            if (request_path.StartsWithSegments("/favicon.ico"))
            {
                return false;
            }

            return true;
        }
    }
}
