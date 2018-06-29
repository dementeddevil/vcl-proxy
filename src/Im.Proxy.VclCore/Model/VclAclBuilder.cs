using System.Collections.Generic;

namespace Im.Proxy.VclCore.Model
{
    public class VclAclBuilder
    {
        private readonly IList<VclAclEntry> _includes = new List<VclAclEntry>();
        private readonly IList<VclAclEntry> _excludes = new List<VclAclEntry>();
        private string _name;

        public VclAclBuilder Include(VclAclEntry entry)
        {
            _includes.Add(entry);
            return this;
        }

        public VclAclBuilder Exclude(VclAclEntry entry)
        {
            _excludes.Add(entry);
            return this;
        }

        public VclAclBuilder SetName(string name)
        {
            _name = name;
            return this;
        }

        public VclAcl Build()
        {
            var acl = new VclAcl(_name);

            foreach (var entry in _includes)
            {
                acl.Include.Add(entry);
            }

            foreach (var entry in _excludes)
            {
                acl.Exclude.Add(entry);
            }

            return acl;
        }
    }
}