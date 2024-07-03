// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Standard headers
#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include <assert.h>
#include <chrono>
#include <iostream>
#include <thread>
#include <vector>

#pragma comment(lib, "libnethost.lib")

#include <nethost.h>
#include <coreclr_delegates.h>
#include <hostfxr.h>

#include <Windows.h>

#ifdef UNICODE
#define STR(s) L ## s
#define CH(c) L ## c
#else
#define STR(s) s
#define CH(c) c
#endif


#define DIR_SEPARATOR TEXT('\\')

using string_t = std::basic_string<char_t>;

namespace
{
	// Globals to hold hostfxr exports
	hostfxr_initialize_for_dotnet_command_line_fn init_for_cmd_line_fptr;
	hostfxr_initialize_for_runtime_config_fn init_for_config_fptr;
	hostfxr_get_runtime_delegate_fn get_delegate_fptr;
	hostfxr_run_app_fn run_app_fptr;
	hostfxr_close_fn close_fptr;

	// Forward declarations
	bool load_hostfxr(const char_t* app);
	int run_app(const string_t& root_path);
}

void wait_dbg_spin_lock(int timeoutMs);

int __cdecl wmain(int argc, wchar_t* argv[])
{
	if (argc > 1)
	{
		auto args1 = std::wstring(argv[1]);
		if (args1 == L"--debug")
		{
			std::wcout << L"Waiting for debugger";
			wait_dbg_spin_lock(20000);
		}
	}

	// Get the current executable's directory
	// This sample assumes the managed assembly to load and its runtime configuration file are next to the host
	char_t host_path[MAX_PATH];
	auto size = ::GetFullPathNameW(argv[0], sizeof(host_path) / sizeof(char_t), host_path, nullptr);
	assert(size != 0);

	string_t root_path = host_path;
	auto pos = root_path.find_last_of(DIR_SEPARATOR);
	assert(pos != string_t::npos);
	root_path = root_path.substr(0, pos + 1);

	return run_app(root_path);
}

namespace
{
	int run_app(const string_t& root_path)
	{
		const string_t app_path = root_path + STR("Carnal Instinct Launcher.dll");

		std::cout << "\x1B[35m"
			"   _____                       _   _____           _   _            _     _                        _               \n"
			"  / ____|                     | | |_   _|         | | (_)          | |   | |                      | |              \n"
			" | |     __ _ _ __ _ __   __ _| |   | |  _ __  ___| |_ _ _ __   ___| |_  | | __ _ _   _ _ __   ___| |__   ___ _ __ \n"
			" | |    / _` | '__| '_ \\ / _` | |   | | | '_ \\/ __| __| | '_ \\ / __| __| | |/ _` | | | | '_ \\ / __| '_ \\ / _ \\ '__|\n"
			" | |___| (_| | |  | | | | (_| | |  _| |_| | | \\__ \\ |_| | | | | (__| |_  | | (_| | |_| | | | | (__| | | |  __/ |   \n"
			" \\_____\\__,_|_|  |_| |_|\\__,_|_| |_____|_| |_|___/\\__|_|_| |_|\\___|\\__| |_|\\__,_|\\__,_|_| |_|\\___|_| |_|\\___|_|    \n"
			"                                                                                                                   \n"
			"                                                                                                                   \n""\033[0m";

		std::wcout << "Loading: " << "\x1B[36m" << app_path << "\033[0m" << std::endl;
		if (!load_hostfxr(app_path.c_str()))
		{
			std::cerr << "\x1B[31mFailure: load_hostfxr()\033[0m\n" << std::endl;
			assert(false && "Failure: load_hostfxr()");
			return EXIT_FAILURE;
		}

		std::cout << "Loading .NET Core" << std::endl;
		// Load .NET Core
		hostfxr_handle cxt = nullptr;
		std::vector<const char_t*> args{ app_path.c_str() };
		int rc = init_for_cmd_line_fptr((int)args.size(), args.data(), nullptr, &cxt);
		if (rc != 0 || cxt == nullptr)
		{
			std::cerr << "\x1B[31mInit failed: " << std::hex << std::showbase << rc << "\033[0m" << std::endl;
			close_fptr(cxt);
			return EXIT_FAILURE;
		}

		std::cout << "Starting .NET Core application " << std::endl;
		// Run the app
		run_app_fptr(cxt);

		close_fptr(cxt);
		return EXIT_SUCCESS;
	}
}


/********************************************************************************************
 * Function used to load and activate .NET Core
 ********************************************************************************************/

namespace
{
	// Forward declarations
	void* load_library(const char_t*);
	void* get_export(void*, const char*);

	void* load_library(const char_t* path)
	{
		HMODULE h = ::LoadLibraryW(path);
		assert(h != nullptr);
		return (void*)h;
	}

	void* get_export(void* h, const char* name)
	{
		void* f = ::GetProcAddress((HMODULE)h, name);
		assert(f != nullptr);
		return f;
	}

	// Using the nethost library, discover the location of hostfxr and get exports
	bool load_hostfxr(const char_t* assembly_path)
	{
		get_hostfxr_parameters params{ sizeof(get_hostfxr_parameters), assembly_path, nullptr };
		// Pre-allocate a large buffer for the path to hostfxr
		char_t buffer[MAX_PATH];
		size_t buffer_size = sizeof(buffer) / sizeof(char_t);
		int rc = get_hostfxr_path(buffer, &buffer_size, &params);
		if (rc != 0)
			return false;

		// Load hostfxr and get desired exports
		// NOTE: The .NET Runtime does not support unloading any of its native libraries. Running
		// dlclose/FreeLibrary on any .NET libraries produces undefined behavior.
		void* lib = load_library(buffer);
		init_for_cmd_line_fptr = (hostfxr_initialize_for_dotnet_command_line_fn)get_export(lib, "hostfxr_initialize_for_dotnet_command_line");
		std::cout << "get_export [ini_for_cmd_line_fptr]: " << "\x1B[32m" <<  std::hex << std::showbase << init_for_cmd_line_fptr << "\033[0m" << std::endl;

		init_for_config_fptr = (hostfxr_initialize_for_runtime_config_fn)get_export(lib, "hostfxr_initialize_for_runtime_config");
		std::cout << "get_export [ini_for_config_fptr]: " << "\x1B[32m" << std::hex << std::showbase << init_for_config_fptr << "\033[0m" << std::endl;

		get_delegate_fptr = (hostfxr_get_runtime_delegate_fn)get_export(lib, "hostfxr_get_runtime_delegate");
		std::cout << "get_export [get_delegate_fptr]: " << "\x1B[32m" << std::hex << std::showbase << get_delegate_fptr << "\033[0m" << std::endl;

		run_app_fptr = (hostfxr_run_app_fn)get_export(lib, "hostfxr_run_app");
		std::cout << "get_export [run_app_fptr]: " << "\x1B[32m" << std::hex << std::showbase << run_app_fptr << "\033[0m" << std::endl;

		close_fptr = (hostfxr_close_fn)get_export(lib, "hostfxr_close");
		std::cout << "get_export [close_fptr]: " << "\x1B[32m" << std::hex << std::showbase << close_fptr << "\033[0m" << std::endl;

		return (init_for_config_fptr && get_delegate_fptr && close_fptr);
	}
}

void wait_dbg_spin_lock(int timeoutMs)
{
	while (!::IsDebuggerPresent() && timeoutMs > 0)
	{
		std::this_thread::sleep_for(std::chrono::milliseconds(100));
		timeoutMs -= 100;
	}
}