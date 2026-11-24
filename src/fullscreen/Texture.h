#pragma once
#include <d3d9.h>
inline HRESULT CreateOverlayTexture(IDirect3DDevice9* device,UINT width,UINT height,IDirect3DTexture9** texture){
 return device->CreateTexture(width,height,1,D3DUSAGE_DYNAMIC,D3DFMT_A8R8G8B8,D3DPOOL_DEFAULT,texture,nullptr);
}
