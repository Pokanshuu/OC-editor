// Prevents additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

/// WebView2 默认把用户数据目录放在 `%LOCALAPPDATA%\<identifier>\EBWebView`。
/// 部分环境（安全软件、受限沙箱、漫游配置文件）不允许往 AppData 写，
/// 表现是窗口一闪就退出、`tauri dev` 看不到任何报错。
/// 开发模式下先探测默认目录可写性，不可写就把用户数据目录放到 target 下。
#[cfg(all(debug_assertions, windows))]
fn use_writable_webview_data_dir() {
  use std::path::PathBuf;

  if std::env::var_os("WEBVIEW2_USER_DATA_FOLDER").is_some() {
    return;
  }

  let writable = |dir: &PathBuf| -> bool {
    if std::fs::create_dir_all(dir).is_err() {
      return false;
    }
    let probe = dir.join(".write-probe");
    let ok = std::fs::write(&probe, b"1").is_ok();
    let _ = std::fs::remove_file(&probe);
    ok
  };

  let default_dir = std::env::var_os("LOCALAPPDATA")
    .map(|base| PathBuf::from(base).join("com.oceditor.app").join("EBWebView"));

  if let Some(dir) = default_dir {
    if writable(&dir) {
      return;
    }
    eprintln!("[warn] WebView2 默认数据目录不可写，改用项目内目录: {dir:?}");
  }

  let fallback = std::env::current_exe()
    .ok()
    .and_then(|exe| exe.parent().map(|dir| dir.join("webview-data")))
    .unwrap_or_else(|| PathBuf::from("webview-data"));

  if std::fs::create_dir_all(&fallback).is_ok() {
    eprintln!("[warn] WebView2 用户数据目录: {fallback:?}");
    std::env::set_var("WEBVIEW2_USER_DATA_FOLDER", fallback);
  }
}

fn main() {
  #[cfg(all(debug_assertions, windows))]
  use_writable_webview_data_dir();

  app_lib::run();
}
