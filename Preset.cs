using System;

public class Preset
{
    public string Name { get; set; } = "";
    public List<string> Domains { get; set; } = new();
    public List<string> Auto_Varients { get; set; } = new();
    public bool Ipv6 { get; set; } = true;
}