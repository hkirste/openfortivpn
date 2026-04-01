/*
 * Wintun API declarations for dynamic loading.
 * Based on the Wintun API (https://www.wintun.net/).
 * The actual wintun.dll is loaded at runtime via LoadLibrary.
 *
 * Wintun is licensed under the MIT license.
 */

#ifndef OPENFORTIVPN_WINTUN_H
#define OPENFORTIVPN_WINTUN_H

#ifdef _WIN32

#include <windows.h>
#include <ipexport.h>
#include <ifdef.h>

typedef void *WINTUN_ADAPTER_HANDLE;
typedef void *WINTUN_SESSION_HANDLE;

typedef WINTUN_ADAPTER_HANDLE(WINAPI *WINTUN_CREATE_ADAPTER_FUNC)(
        const WCHAR *Name, const WCHAR *TunnelType,
        const GUID *RequestedGUID);

typedef void(WINAPI *WINTUN_CLOSE_ADAPTER_FUNC)(
        WINTUN_ADAPTER_HANDLE Adapter);

typedef WINTUN_SESSION_HANDLE(WINAPI *WINTUN_START_SESSION_FUNC)(
        WINTUN_ADAPTER_HANDLE Adapter, DWORD Capacity);

typedef void(WINAPI *WINTUN_END_SESSION_FUNC)(
        WINTUN_SESSION_HANDLE Session);

typedef HANDLE(WINAPI *WINTUN_GET_READ_WAIT_EVENT_FUNC)(
        WINTUN_SESSION_HANDLE Session);

typedef BYTE *(WINAPI *WINTUN_RECEIVE_PACKET_FUNC)(
        WINTUN_SESSION_HANDLE Session, DWORD *PacketSize);

typedef void(WINAPI *WINTUN_RELEASE_RECEIVE_PACKET_FUNC)(
        WINTUN_SESSION_HANDLE Session, const BYTE *Packet);

typedef BYTE *(WINAPI *WINTUN_ALLOCATE_SEND_PACKET_FUNC)(
        WINTUN_SESSION_HANDLE Session, DWORD PacketSize);

typedef void(WINAPI *WINTUN_SEND_PACKET_FUNC)(
        WINTUN_SESSION_HANDLE Session, const BYTE *Packet);

typedef void(WINAPI *WINTUN_GET_ADAPTER_LUID_FUNC)(
        WINTUN_ADAPTER_HANDLE Adapter, NET_LUID *Luid);

typedef DWORD(WINAPI *WINTUN_GET_RUNNING_DRIVER_VERSION_FUNC)(void);

/* Function pointer table loaded from wintun.dll */
struct wintun_api {
	HMODULE module;
	WINTUN_CREATE_ADAPTER_FUNC CreateAdapter;
	WINTUN_CLOSE_ADAPTER_FUNC CloseAdapter;
	WINTUN_START_SESSION_FUNC StartSession;
	WINTUN_END_SESSION_FUNC EndSession;
	WINTUN_GET_READ_WAIT_EVENT_FUNC GetReadWaitEvent;
	WINTUN_RECEIVE_PACKET_FUNC ReceivePacket;
	WINTUN_RELEASE_RECEIVE_PACKET_FUNC ReleaseReceivePacket;
	WINTUN_ALLOCATE_SEND_PACKET_FUNC AllocateSendPacket;
	WINTUN_SEND_PACKET_FUNC SendPacket;
	WINTUN_GET_ADAPTER_LUID_FUNC GetAdapterLUID;
	WINTUN_GET_RUNNING_DRIVER_VERSION_FUNC GetRunningDriverVersion;
};

/*
 * Load wintun.dll and resolve all function pointers.
 * Returns 0 on success, -1 on failure.
 */
static inline FARPROC wintun_resolve(HMODULE mod,
                                     const char *name)
{
	return GetProcAddress(mod, name);
}

static inline int wintun_load_funcs(struct wintun_api *api,
                                    HMODULE mod)
{
	FARPROC fn;

	fn = wintun_resolve(mod, "WintunCreateAdapter");
	if (!fn)
		return 0;
	api->CreateAdapter = (WINTUN_CREATE_ADAPTER_FUNC)fn;

	fn = wintun_resolve(mod, "WintunCloseAdapter");
	if (!fn)
		return 0;
	api->CloseAdapter = (WINTUN_CLOSE_ADAPTER_FUNC)fn;

	fn = wintun_resolve(mod, "WintunStartSession");
	if (!fn)
		return 0;
	api->StartSession = (WINTUN_START_SESSION_FUNC)fn;

	fn = wintun_resolve(mod, "WintunEndSession");
	if (!fn)
		return 0;
	api->EndSession = (WINTUN_END_SESSION_FUNC)fn;

	fn = wintun_resolve(mod, "WintunGetReadWaitEvent");
	if (!fn)
		return 0;
	api->GetReadWaitEvent = (WINTUN_GET_READ_WAIT_EVENT_FUNC)fn;

	fn = wintun_resolve(mod, "WintunReceivePacket");
	if (!fn)
		return 0;
	api->ReceivePacket = (WINTUN_RECEIVE_PACKET_FUNC)fn;

	fn = wintun_resolve(mod, "WintunReleaseReceivePacket");
	if (!fn)
		return 0;
	api->ReleaseReceivePacket = (WINTUN_RELEASE_RECEIVE_PACKET_FUNC)fn;

	fn = wintun_resolve(mod, "WintunAllocateSendPacket");
	if (!fn)
		return 0;
	api->AllocateSendPacket = (WINTUN_ALLOCATE_SEND_PACKET_FUNC)fn;

	fn = wintun_resolve(mod, "WintunSendPacket");
	if (!fn)
		return 0;
	api->SendPacket = (WINTUN_SEND_PACKET_FUNC)fn;

	fn = wintun_resolve(mod, "WintunGetAdapterLUID");
	if (!fn)
		return 0;
	api->GetAdapterLUID = (WINTUN_GET_ADAPTER_LUID_FUNC)fn;

	fn = wintun_resolve(mod, "WintunGetRunningDriverVersion");
	if (!fn)
		return 0;
	api->GetRunningDriverVersion =
	        (WINTUN_GET_RUNNING_DRIVER_VERSION_FUNC)fn;

	return 1;
}

static inline int wintun_load(struct wintun_api *api)
{
	HMODULE mod = LoadLibraryW(L"wintun.dll");

	if (!mod)
		return -1;

	api->module = mod;

	if (!wintun_load_funcs(api, mod)) {
		FreeLibrary(mod);
		return -1;
	}

	return 0;
}

static inline void wintun_unload(struct wintun_api *api)
{
	if (api->module) {
		FreeLibrary(api->module);
		api->module = NULL;
	}
}

#endif /* _WIN32 */

#endif /* OPENFORTIVPN_WINTUN_H */
