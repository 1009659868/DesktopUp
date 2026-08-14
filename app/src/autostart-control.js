export const AUTOSTART_STATE_EVENT = "autostart-state-changed";

export function deriveAutostartControl(snapshot, pending = false) {
  if (!snapshot) {
    return {
      checked: false,
      disabled: true,
      status: "正在读取原生状态",
      state: "loading"
    };
  }

  const registrationState = String(snapshot.registrationState ?? "unsupported");
  const eligible = snapshot.registrationEligible === true;
  const checked = snapshot.effectiveEnabled === true;
  if (pending) {
    return {
      checked,
      disabled: true,
      status: "正在写入并读回确认",
      state: "pending"
    };
  }
  if (!eligible) {
    return {
      checked: false,
      disabled: true,
      status: "仅正式安装版可设置",
      state: "ineligible"
    };
  }
  if (registrationState === "unreadable" || registrationState === "unsupported") {
    return {
      checked,
      disabled: true,
      status: "注册状态不可读，未作更改",
      state: "unreadable"
    };
  }
  if (registrationState === "drifted") {
    return {
      checked: false,
      disabled: false,
      status: "配置异常；开启可按正式命令修复",
      state: "drifted"
    };
  }
  return {
    checked,
    disabled: false,
    status: checked ? "已启用并通过读回确认" : "已关闭",
    state: registrationState
  };
}

export function autostartOutcomeMessage(result) {
  if (result?.succeeded) {
    if (result.outcome === "repaired") return "开机自启配置已修复并确认";
    if (result.outcome === "enabled") return "开机自启已启用并确认";
    if (result.outcome === "disabled") return "开机自启已关闭并确认";
    return result.requestedEnabled ? "开机自启已经处于启用状态" : "开机自启已经处于关闭状态";
  }
  if (result?.outcome === "rejectedIneligible") return "仅正式安装版允许更改开机自启";
  if (result?.outcome === "rejectedUnreadable") return "注册状态不可读，未更改开机自启";
  if (result?.outcome === "verificationFailed") return "写入后的状态未通过确认，界面已恢复为读回结果";
  return "开机自启更改失败，界面已恢复为读回结果";
}
