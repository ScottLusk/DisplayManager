namespace Gregghz.DisplayManager.Windows.Native;

public static class Constants
{
  public const int ENUM_CURRENT_SETTINGS = -1;
  public const int ENUM_REGISTRY_SETTINGS = -2;

  public const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;
  public const int DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004;

  public const int DM_POSITION = 0x00000020;
  public const int DM_DISPLAYORIENTATION = 0x00000080;
  public const int DM_PELSWIDTH = 0x00080000;
  public const int DM_PELSHEIGHT = 0x00100000;
  public const int DM_DISPLAYFREQUENCY = 0x00400000;

  public const uint SDC_APPLY = 0x00000080;
  public const uint SDC_TOPOLOGY_EXTEND = 0x00000004;

  public const int CDS_NONE = 0x00;
  public const int CDS_UPDATEREGISTRY = 0x01;
  public const int CDS_SET_PRIMARY = 0x10;
  public const int CDS_NORESET = 0x10000000;

  public const int DISP_CHANGE_SUCCESSFUL = 0;
  public const int DISP_CHANGE_RESTART = 1;
  public const int DISP_CHANGE_FAILED = -1;
  public const int DISP_CHANGE_BADMODE = -2;
  public const int DISP_CHANGE_NOTUPDATED = -3;
  public const int DISP_CHANGE_BADFLAGS = -4;
  public const int DISP_CHANGE_BADPARAM = -5;
  public const int DISP_CHANGE_BADDUALVIEW = -6;
}