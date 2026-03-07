using System;
using System.Collections.Generic;

namespace MasterPlay.Models.Models;

public partial class DetectedProcess
{
    public int Id { get; set; }

    public string ExeName { get; set; } = null!;

    public string FullPath { get; set; } = null!;
}
