﻿namespace Ominify
{
    public class SimpleCssPackage : OminifyPackage
    {
        public SimpleCssPackage(string packagePath) : base(packagePath)
        {
        }

        public override string GetContentType()
        {
            return "text/css";
        }

        public override string GetHtmlElement(string url)
        {
            return string.Format("<link rel=\"stylesheet\" href=\"{0}\" />", url);
        }
    }
}