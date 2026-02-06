using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class GroupRoleInfo
{
    public string RoleId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool CanManageMembers { get; set; }
    public bool CanModerate { get; set; }
}

