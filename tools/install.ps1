param($installPath, $toolsPath, $package, $project)

$project.ProjectItems.Item('EasyHook32.dll').Properties.Item("CopyToOutputDirectory").Value = [int]2;
$project.ProjectItems.Item('EasyHook64.dll').Properties.Item("CopyToOutputDirectory").Value = [int]2;
$project.ProjectItems.Item('EasyLoad32.dll').Properties.Item("CopyToOutputDirectory").Value = [int]2;
$project.ProjectItems.Item('EasyLoad64.dll').Properties.Item("CopyToOutputDirectory").Value = [int]2;
$project.ProjectItems.Item('EasyHook32Svc.exe').Properties.Item("CopyToOutputDirectory").Value = [int]2;
$project.ProjectItems.Item('EasyHook64Svc.exe').Properties.Item("CopyToOutputDirectory").Value = [int]2;