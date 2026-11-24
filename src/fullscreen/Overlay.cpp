#include <windows.h>
#include <d3d9.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include "MinHook.h"
#include "Texture.h"
// ABI v1, shared with FullscreenOverlay.cs. All fields are 32-bit, pixels are premultiplied BGRA.
const int Slot=2048*2048*4, Header=128;
struct Surface {int width,height,x,y,revision;};
struct Shared {int magic,version,enabled;DWORD heartbeat;int status,width,height,windowed;Surface surfaces[2];};
static_assert(sizeof(Shared)<=Header,"header overflow");
static Shared* shared;static HANDLE mutexHandle;static HRESULT (WINAPI *original)(IDirect3DDevice9*);
static IDirect3DTexture9* textures[2];static int revisions[2]={-1,-1};static IDirect3DDevice9* lastDevice;
struct Vertex {float x,y,z,rhw;DWORD color;float u,v;};
static HRESULT DrawSurface(IDirect3DDevice9* device,int index,int width,int height){
 const Surface& s=shared->surfaces[index];
 if(s.width<=0||s.height<=0||s.width>2048||s.height>2048)return S_FALSE;
 if(revisions[index]!=s.revision){
  if(textures[index]){textures[index]->Release();textures[index]=nullptr;}
  HRESULT hr=CreateOverlayTexture(device,s.width,s.height,&textures[index]);if(FAILED(hr))return hr;
  D3DLOCKED_RECT rect;hr=textures[index]->LockRect(0,&rect,nullptr,D3DLOCK_DISCARD);if(FAILED(hr))return hr;
  BYTE* source=(BYTE*)shared+Header+index*Slot;
  for(int row=0;row<s.height;row++)memcpy((BYTE*)rect.pBits+row*rect.Pitch,source+row*s.width*4,s.width*4);
  textures[index]->UnlockRect(0);revisions[index]=s.revision;
 }
 if(!textures[index])return E_FAIL;
 int px=index==0?width/2-s.width/2+s.x:s.x,py=index==0?height/2-s.height/2+s.y:s.y;
 if(index==1){px=max(0,min(px,width-s.width));py=max(0,min(py,height-s.height));}
 float x=px-.5f,y=py-.5f,w=(float)s.width,h=(float)s.height;
 Vertex v[]={{x,y,0,1,0xffffffff,0,0},{x+w,y,0,1,0xffffffff,1,0},{x,y+h,0,1,0xffffffff,0,1},{x+w,y+h,0,1,0xffffffff,1,1}};
 HRESULT hr=device->SetTexture(0,textures[index]);if(FAILED(hr))return hr;return device->DrawPrimitiveUP(D3DPT_TRIANGLESTRIP,2,v,sizeof(Vertex));
}
static HRESULT WINAPI Draw(IDirect3DDevice9* d){
 DWORD wait=WaitForSingleObject(mutexHandle,0);
 if(wait==WAIT_OBJECT_0||wait==WAIT_ABANDONED){
  if(shared->magic==0x52584653&&shared->version==1&&shared->enabled && DWORD(GetTickCount()-shared->heartbeat)<5000){
   IDirect3DSurface9 *target=nullptr,*back=nullptr;d->GetRenderTarget(0,&target);d->GetBackBuffer(0,0,D3DBACKBUFFER_TYPE_MONO,&back);
   if(target&&target==back){
    D3DSURFACE_DESC desc={};back->GetDesc(&desc);shared->width=desc.Width;shared->height=desc.Height;
    IDirect3DSwapChain9* chain=nullptr;D3DPRESENT_PARAMETERS pp={};if(SUCCEEDED(d->GetSwapChain(0,&chain))){chain->GetPresentParameters(&pp);chain->Release();shared->windowed=pp.Windowed;}
    if(lastDevice!=d){for(int i=0;i<2;i++){if(textures[i])textures[i]->Release();textures[i]=nullptr;revisions[i]=-1;}lastDevice=d;}
    // Only replace external overlays when the game itself requests fullscreen.
    if(!pp.Windowed){
     IDirect3DStateBlock9* state=nullptr;
     if(SUCCEEDED(d->CreateStateBlock(D3DSBT_ALL,&state))&&SUCCEEDED(state->Capture())){
      D3DVIEWPORT9 viewport={0,0,desc.Width,desc.Height,0,1};d->SetViewport(&viewport);
      d->SetVertexShader(nullptr);d->SetPixelShader(nullptr);d->SetFVF(D3DFVF_XYZRHW|D3DFVF_DIFFUSE|D3DFVF_TEX1);
      d->SetRenderState(D3DRS_ZENABLE,FALSE);d->SetRenderState(D3DRS_ZWRITEENABLE,FALSE);d->SetRenderState(D3DRS_ALPHATESTENABLE,FALSE);
      d->SetRenderState(D3DRS_CULLMODE,D3DCULL_NONE);d->SetRenderState(D3DRS_LIGHTING,FALSE);d->SetRenderState(D3DRS_FOGENABLE,FALSE);d->SetRenderState(D3DRS_STENCILENABLE,FALSE);d->SetRenderState(D3DRS_SCISSORTESTENABLE,FALSE);
      d->SetRenderState(D3DRS_ALPHABLENDENABLE,TRUE);d->SetRenderState(D3DRS_SRCBLEND,D3DBLEND_ONE);d->SetRenderState(D3DRS_DESTBLEND,D3DBLEND_INVSRCALPHA);d->SetRenderState(D3DRS_BLENDOP,D3DBLENDOP_ADD);d->SetRenderState(D3DRS_SEPARATEALPHABLENDENABLE,FALSE);d->SetRenderState(D3DRS_COLORWRITEENABLE,15);d->SetRenderState(D3DRS_SRGBWRITEENABLE,FALSE);
      d->SetTextureStageState(0,D3DTSS_COLOROP,D3DTOP_SELECTARG1);d->SetTextureStageState(0,D3DTSS_COLORARG1,D3DTA_TEXTURE);d->SetTextureStageState(0,D3DTSS_ALPHAOP,D3DTOP_SELECTARG1);d->SetTextureStageState(0,D3DTSS_ALPHAARG1,D3DTA_TEXTURE);d->SetTextureStageState(1,D3DTSS_COLOROP,D3DTOP_DISABLE);d->SetTextureStageState(0,D3DTSS_TEXCOORDINDEX,0);d->SetTextureStageState(0,D3DTSS_TEXTURETRANSFORMFLAGS,D3DTTFF_DISABLE);
      d->SetSamplerState(0,D3DSAMP_MINFILTER,D3DTEXF_POINT);d->SetSamplerState(0,D3DSAMP_MAGFILTER,D3DTEXF_POINT);d->SetSamplerState(0,D3DSAMP_MIPFILTER,D3DTEXF_NONE);d->SetSamplerState(0,D3DSAMP_ADDRESSU,D3DTADDRESS_CLAMP);d->SetSamplerState(0,D3DSAMP_ADDRESSV,D3DTADDRESS_CLAMP);d->SetSamplerState(0,D3DSAMP_SRGBTEXTURE,FALSE);
      HRESULT first=DrawSurface(d,0,desc.Width,desc.Height),second=DrawSurface(d,1,desc.Width,desc.Height);state->Apply();
      HRESULT result=FAILED(first)?first:second;*((HRESULT*)((BYTE*)shared+76))=result;
      shared->status=FAILED(result)?-3:(first==S_FALSE&&second==S_FALSE?3:2);
      if(shared->status==2)*((DWORD*)((BYTE*)shared+72))=GetTickCount();
      // DEFAULT-pool resources must not survive Reset/ResetEx. Release each frame
      // so toggling resolution or Alt+Tab never leaves our textures blocking Reset.
      for(int i=0;i<2;i++){if(textures[i]){textures[i]->Release();textures[i]=nullptr;}revisions[i]=-1;}
     }
     if(state)state->Release();
    }else shared->status=1;
   }
   if(target)target->Release();if(back)back->Release();
  }
  ReleaseMutex(mutexHandle);
 }
 return original(d);
}
static DWORD WINAPI Run(void* module){
 wchar_t name[100];swprintf_s(name,L"Local\\RxFullscreen-v1-%lu",GetCurrentProcessId());HANDLE mapping=OpenFileMappingW(FILE_MAP_ALL_ACCESS,FALSE,name);if(!mapping)return 1;
 shared=(Shared*)MapViewOfFile(mapping,FILE_MAP_ALL_ACCESS,0,0,Header+Slot*2);if(!shared){CloseHandle(mapping);return 2;}
 swprintf_s(name,L"Local\\RxFullscreen-lock-v1-%lu",GetCurrentProcessId());mutexHandle=OpenMutexW(SYNCHRONIZE|MUTEX_MODIFY_STATE,FALSE,name);if(!mutexHandle)return 3;
 WNDCLASSW wc={};wc.lpfnWndProc=DefWindowProcW;wc.hInstance=(HINSTANCE)module;wc.lpszClassName=L"RxFullscreenDeviceV1";RegisterClassW(&wc);
 HWND window=CreateWindowW(wc.lpszClassName,L"",WS_OVERLAPPED,0,0,64,64,nullptr,nullptr,wc.hInstance,nullptr);
 IDirect3D9* api=Direct3DCreate9(D3D_SDK_VERSION);IDirect3DDevice9* device=nullptr;D3DPRESENT_PARAMETERS pp={};pp.Windowed=TRUE;pp.SwapEffect=D3DSWAPEFFECT_DISCARD;pp.hDeviceWindow=window;
 HRESULT hr=api?api->CreateDevice(0,D3DDEVTYPE_NULLREF,window,D3DCREATE_SOFTWARE_VERTEXPROCESSING,&pp,&device):E_FAIL;
 if(FAILED(hr)){shared->status=-1;if(api)api->Release();DestroyWindow(window);return 4;}
 void* address=(*(void***)device)[42];device->Release();api->Release();DestroyWindow(window);UnregisterClassW(wc.lpszClassName,wc.hInstance);
 if(MH_Initialize()!=MH_OK||MH_CreateHook(address,Draw,(void**)&original)!=MH_OK||MH_EnableHook(address)!=MH_OK){shared->status=-2;return 5;}
 shared->status=1;return 0;
}
BOOL WINAPI DllMain(HINSTANCE module,DWORD reason,LPVOID){if(reason==DLL_PROCESS_ATTACH){DisableThreadLibraryCalls(module);HANDLE t=CreateThread(nullptr,0,Run,module,0,nullptr);if(t)CloseHandle(t);}return TRUE;}
