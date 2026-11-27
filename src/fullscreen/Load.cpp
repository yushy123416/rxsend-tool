#include <windows.h>
#include <tlhelp32.h>
#include <stdio.h>
#include <wchar.h>
#include "GameTarget.h"
int wmain(int argc,wchar_t** argv){
 if(argc!=3){wprintf(L"Usage: Load.exe PID Overlay.dll\n");return 1;}
 DWORD pid=wcstoul(argv[1],nullptr,10);HANDLE p=OpenProcess(PROCESS_CREATE_THREAD|PROCESS_QUERY_INFORMATION|PROCESS_VM_OPERATION|PROCESS_VM_WRITE|PROCESS_VM_READ,FALSE,pid);
 if(!p){printf("OpenProcess failed %lu\n",GetLastError());return 2;}
 if(!IsAllowedGameProcess(p)){puts("Only verified x64 GMod (gmod.exe or gmod_win64.exe) is allowed");CloseHandle(p);return 3;}
 HMODULE local=GetModuleHandleW(L"kernel32.dll");auto proc=GetProcAddress(local,"LoadLibraryW");HMODULE owner=nullptr;
 GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,(LPCWSTR)proc,&owner);
 wchar_t ownerPath[MAX_PATH];GetModuleFileNameW(owner,ownerPath,MAX_PATH);const wchar_t* ownerName=wcsrchr(ownerPath,L'\\')+1;
 HANDLE snap=CreateToolhelp32Snapshot(TH32CS_SNAPMODULE,pid);MODULEENTRY32W entry={sizeof(entry)};BYTE* remoteBase=nullptr;
 if(Module32FirstW(snap,&entry))do{if(!_wcsicmp(entry.szModule,ownerName))remoteBase=entry.modBaseAddr;if(!_wcsicmp(entry.szModule,(wcsrchr(argv[2],L'\\')?wcsrchr(argv[2],L'\\')+1:argv[2]))){bool same=!_wcsicmp(entry.szExePath,argv[2]);puts(same?"Overlay already loaded":"Different overlay version loaded; restart GMod");CloseHandle(snap);CloseHandle(p);return same?0:4;}}while(Module32NextW(snap,&entry));CloseHandle(snap);
 if(!remoteBase){CloseHandle(p);return 5;}
 wchar_t path[MAX_PATH];if(!GetFullPathNameW(argv[2],MAX_PATH,path,nullptr)||GetFileAttributesW(path)==INVALID_FILE_ATTRIBUTES){CloseHandle(p);return 6;}
 SIZE_T bytes=(wcslen(path)+1)*sizeof(wchar_t);void* memory=VirtualAllocEx(p,nullptr,bytes,MEM_COMMIT|MEM_RESERVE,PAGE_READWRITE);
 if(!memory||!WriteProcessMemory(p,memory,path,bytes,nullptr)){puts("Write failed");CloseHandle(p);return 7;}
 HANDLE thread=CreateRemoteThread(p,nullptr,0,(LPTHREAD_START_ROUTINE)(remoteBase+((BYTE*)proc-(BYTE*)owner)),memory,0,nullptr);
 if(!thread){VirtualFreeEx(p,memory,0,MEM_RELEASE);CloseHandle(p);return 8;}
 DWORD wait=WaitForSingleObject(thread,15000);DWORD result=0;if(wait==WAIT_OBJECT_0){GetExitCodeThread(thread,&result);VirtualFreeEx(p,memory,0,MEM_RELEASE);}CloseHandle(thread);CloseHandle(p);
 printf("Load finished: wait=%lu result=%lu; inspect shared overlay status for actual rendering\n",wait,result);return wait==WAIT_OBJECT_0&&result?0:9;
}
