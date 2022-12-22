using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tool
{
    internal class NuGetPackageInfo : IComparable<NuGetPackageInfo>, IEquatable<NuGetPackageInfo>
    {
        public string Id { get; set; }
        public string Version { get; set; }

        public NuGetPackageInfo(string id, string version)
        {
            this.Id = id;
            this.Version = version;
        }

        public int CompareTo(NuGetPackageInfo? other)
        {
            if (this.Version.Equals(other?.Version, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var thisVersionDashSegments = this.Version.Split('-');
            var otherVersionDashSegments = other?.Version.Split('-') ?? Array.Empty<string>();

            var thisVersion = System.Version.Parse(thisVersionDashSegments.First());
            var otherVersion = System.Version.Parse(otherVersionDashSegments.First());
            var compareVersions = thisVersion.CompareTo(otherVersion);

            if (compareVersions != 0)
            {
                return compareVersions;
            }

            var thisBetaPortion = thisVersionDashSegments.Length == 1 ? string.Empty : thisVersionDashSegments[1].ToLowerInvariant();
            var otherBetaPortion = otherVersionDashSegments.Length == 1 ? string.Empty : otherVersionDashSegments[1].ToLowerInvariant();

            return thisBetaPortion.CompareTo(otherBetaPortion);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as NuGetPackageInfo);
        }

        public bool Equals(NuGetPackageInfo? other)
        {
            if (other == null)
            {
                return false;
            }

            return object.ReferenceEquals(this, other) || (
                   Id.Equals(other.Id, StringComparison.OrdinalIgnoreCase) &&
                   Version.Equals(other.Version, StringComparison.OrdinalIgnoreCase)
            );
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Version);
        }

        public static bool operator ==(NuGetPackageInfo? left, NuGetPackageInfo? right)
        {
            if (left == null && right == null)
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return EqualityComparer<NuGetPackageInfo>.Default.Equals(left, right);
        }

        public static bool operator !=(NuGetPackageInfo left, NuGetPackageInfo right)
        {
            return !(left == right);
        }
    }
}
