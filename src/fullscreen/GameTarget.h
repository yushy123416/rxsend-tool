#pragma once
#include <windows.h>
#include <string>
#include <wchar.h>

inline bool IsSupportedGameImage(const std::wstring& image){
 size_t slash=image.find_last_of(L"\\/");std::wstring name=image.substr(slash==std::wstring::npos?0:slash+1);
 return !_wcsicmp(name.c_str(),L"gmod.exe")||!_wcsicmp(name.c_str(),L"gmod_win64.exe");
}
inline bool HasGameMarker(const std::wstring& image){
 if(!IsSupportedGameImage(image))return false;
 DWORD attributes=GetFileAttributesW(image.c_str());if(attributes==INVALID_FILE_ATTRIBUTES||(attributes&FILE_ATTRIBUTE_DIRECTORY))return false;
 std::wstring directory=image;
 for(int i=0;i<4;i++){
  size_t slash=directory.find_last_of(L"\\/");if(slash==std::wstring::npos)return false;directory.resize(slash);
  std::wstring marker=directory+L"\\garrysmod\\gameinfo.txt";attributes=GetFileAttributesW(marker.c_str());
  if(attributes!=INVALID_FILE_ATTRIBUTES&&!(attributes&FILE_ATTRIBUTE_DIRECTORY))return true;
 }
 return false;
}
inline bool IsAmd64GameProcess(HANDLE process){
 typedef BOOL (WINAPI *Wow64Process2)(HANDLE,USHORT*,USHORT*);
 auto query=(Wow64Process2)GetProcAddress(GetModuleHandleW(L"kernel32.dll"),"IsWow64Process2");
 if(query){USHORT machine=0,native=0;return query(process,&machine,&native)&&(machine?machine:native)==IMAGE_FILE_MACHINE_AMD64;}
 SYSTEM_INFO info={};GetNativeSystemInfo(&info);BOOL wow=FALSE;
 return info.wProcessorArchitecture==PROCESSOR_ARCHITECTURE_AMD64&&IsWow64Process(process,&wow)&&!wow;
}
inline bool IsAllowedGameProcess(HANDLE process){
 if(!IsAmd64GameProcess(process))return false;
 wchar_t image[32768];DWORD count=32768;
 return QueryFullProcessImageNameW(process,0,image,&count)&&HasGameMarker(image);
}
