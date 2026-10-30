function (config) {
  // This script runs only after the native host verifies the configured origin.
  // Check again in-page: navigation could race with ExecuteScriptAsync.
  if (location.origin !== config.origin) return { state: 'redirect', message: '页面来源已变化，自动填写已暂停。请确认认证网址。' };
  const visible = el => !!el && !el.disabled && el.getClientRects().length > 0 &&
    getComputedStyle(el).visibility !== 'hidden' && getComputedStyle(el).display !== 'none';
  const documents = [document];
  for (let index = 0; index < documents.length && index < 12; index++) {
    for (const frame of documents[index].querySelectorAll('iframe,frame')) {
      try { if (frame.contentDocument && !documents.includes(frame.contentDocument)) documents.push(frame.contentDocument); }
      catch (_) { /* A different origin must never receive credentials. */ }
    }
  }
  function find(selector) {
    for (const doc of documents) {
      const found = Array.from(doc.querySelectorAll(selector)).find(visible);
      if (found) return found;
    }
    return null;
  }
  function text(el) { return (el.innerText || el.value || '').trim(); }
  function setValue(el, value) {
    const win = el.ownerDocument.defaultView;
    const descriptor = Object.getOwnPropertyDescriptor(win.HTMLInputElement.prototype, 'value');
    descriptor.set.call(el, value);
    el.dispatchEvent(new win.Event('input', { bubbles: true }));
    el.dispatchEvent(new win.Event('change', { bubbles: true }));
    el.dispatchEvent(new win.Event('blur', { bubbles: true }));
  }
  try {
    const password = find(config.passwordSelector || '#password,input[name="password"],#user_password,input[type="password"]');
    const controls = documents.flatMap(doc => Array.from(doc.querySelectorAll('button,a,input[type="submit"],input[type="button"],[role="button"]'))).filter(visible);
    const logout = controls.find(el => /^(注销|下线|退出登录|断开连接|Logout|Sign out)$/i.test(text(el)));
    if (logout && !password) return { state: 'portal-online', message: '认证页面显示账号已在线。' };

    const errorPattern = /密码.{0,8}(错误|不正确)|用户名.{0,8}(错误|不存在)|账号.{0,8}(错误|不存在|停机|禁用)|用户不存在|余额不足|欠费|认证失败|未开通|连接认证服务器失败|请求.{0,8}频繁|E\d{4}|password.{0,8}(error|incorrect|invalid)/i;
    for (const doc of documents) {
      const notices = Array.from(doc.querySelectorAll('.layui-layer-content,.ant-message-notice-content,.el-message,.alert-danger,#error-message,#login_error,.error-message,[role="alert"]')).filter(visible);
      const failure = notices.map(text).find(value => errorPattern.test(value));
      if (failure) return { state: 'error', message: failure.slice(0, 180) };
    }
    if (!config.submit) return { state: 'waiting', message: '等待认证结果。' };
    const captcha = find('input[name*="captcha" i],input[id*="captcha" i],input[name*="verifycode" i],input[placeholder*="验证码"]');
    if (captcha) return { state: 'manual', message: '认证页需要验证码，请在右侧页面完成登录。' };
    const username = find(config.usernameSelector || '#username,input[name="username"],#user_name,input[name="user_name"],input[autocomplete="username"]') ||
      find('input[type="text"],input[type="tel"],input[type="email"],input:not([type])');
    if (!username || !password) return { state: 'missing', message: '未找到账号或密码输入框。请检查认证网址，或打开设置中的连接页填写对应的 CSS 选择器。' };
    const button = config.loginSelector ? find(config.loginSelector) :
      controls.find(el => /^(登录|登\s*录|上网|连接|立即登录|认证|Login|Sign in)$/i.test(text(el))) || find('#login,#login-account,button[type="submit"],input[type="submit"]');
    if (!button) return { state: 'missing', message: '未找到登录按钮。请在认证页面手动登录，或打开设置中的连接页填写登录按钮 CSS 选择器。' };
    if (config.provider && config.provider.trim()) {
      const wanted = config.provider.trim().toLowerCase();
      const candidates = [];
      for (const doc of documents) {
        for (const select of doc.querySelectorAll('select')) {
          if (!visible(select)) continue;
          for (const option of select.options) {
            if (!option.disabled && (option.text.trim().toLowerCase() === wanted || option.value.trim().toLowerCase() === wanted))
              candidates.push({ select, option });
          }
        }
        for (const radio of doc.querySelectorAll('input[type="radio"]')) {
          if (!visible(radio)) continue;
          const label = radio.closest('label') || Array.from(doc.querySelectorAll('label')).find(el => el.htmlFor && el.htmlFor === radio.id);
          if ((label && text(label).toLowerCase() === wanted) || radio.value.trim().toLowerCase() === wanted)
            candidates.push({ radio });
        }
      }
      if (candidates.length !== 1) return { state: 'manual', message: '未能唯一匹配运营商，请在认证页面选择，或在设置中修改运营商名称。' };
      const choice = candidates[0];
      if (choice.radio) { if (!choice.radio.checked) choice.radio.click(); }
      else if (choice.select.value !== choice.option.value) {
        choice.select.value = choice.option.value;
        const win = choice.select.ownerDocument.defaultView;
        choice.select.dispatchEvent(new win.Event('input', { bubbles: true }));
        choice.select.dispatchEvent(new win.Event('change', { bubbles: true }));
      }
    }
    setValue(username, config.username);
    setValue(password, config.password);
    for (const doc of documents) {
      for (const checkbox of doc.querySelectorAll('input[type="checkbox"]')) {
        const label = checkbox.closest('label') || Array.from(doc.querySelectorAll('label')).find(el => el.htmlFor === checkbox.id);
        if (label && visible(label) && !checkbox.checked && /同意|协议|服务条款|已阅读/.test(text(label))) checkbox.click();
      }
    }
    button.click();
    return { state: 'submitted', message: '已提交校园网认证，正在检查网络。' };
  } catch (error) {
    return { state: 'manual', message: '无法自动填写当前页面，请检查高级设置或在认证页面手动登录。' };
  }
}
