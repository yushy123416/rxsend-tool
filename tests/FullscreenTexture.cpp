#include <windows.h>
#include <stdio.h>
#include "../src/fullscreen/Texture.h"
int main(){
 WNDCLASSW wc={};wc.lpfnWndProc=DefWindowProcW;wc.hInstance=GetModuleHandleW(nullptr);wc.lpszClassName=L"RxTextureRegression";RegisterClassW(&wc);
 HWND window=CreateWindowW(wc.lpszClassName,L"",WS_OVERLAPPED,0,0,64,64,nullptr,nullptr,wc.hInstance,nullptr);
 IDirect3D9Ex* api=nullptr;IDirect3DDevice9Ex* device=nullptr;IDirect3DTexture9* texture=nullptr;
 D3DPRESENT_PARAMETERS pp={};pp.Windowed=TRUE;pp.SwapEffect=D3DSWAPEFFECT_DISCARD;pp.hDeviceWindow=window;pp.BackBufferWidth=64;pp.BackBufferHeight=64;
 HRESULT hr=Direct3DCreate9Ex(D3D_SDK_VERSION,&api);if(FAILED(hr)){printf("SKIP Direct3D9Ex runtime unavailable in this session (0x%08lX)\n",(unsigned long)hr);DestroyWindow(window);return 77;}
 hr=api->CreateDeviceEx(0,D3DDEVTYPE_NULLREF,window,D3DCREATE_SOFTWARE_VERTEXPROCESSING,&pp,nullptr,&device);if(FAILED(hr))return 2;
 hr=device->CreateTexture(64,64,1,0,D3DFMT_A8R8G8B8,D3DPOOL_MANAGED,&texture,nullptr);if(SUCCEEDED(hr))return 3;
 hr=CreateOverlayTexture(device,64,64,&texture);if(FAILED(hr))return 4;
 D3DLOCKED_RECT rect;hr=texture->LockRect(0,&rect,nullptr,D3DLOCK_DISCARD);if(FAILED(hr))return 5;
 memset(rect.pBits,255,64*4);texture->UnlockRect(0);texture->Release();texture=nullptr;
 hr=device->ResetEx(&pp,nullptr);if(FAILED(hr))return 6;
 hr=CreateOverlayTexture(device,360,148,&texture);if(FAILED(hr))return 7;texture->Release();device->Release();api->Release();DestroyWindow(window);UnregisterClassW(wc.lpszClassName,wc.hInstance);
 puts("PASS Direct3D9Ex: managed rejected; overlay texture upload, ResetEx and recreation succeed");return 0;
}
