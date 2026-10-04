import { useMutation } from '@tanstack/react-query';
import { Alert, Button, Form, Input } from 'antd';
import { errorMessage } from '../api/errors';
import { login } from '../api/identity';

type Values = { identifier: string; password: string };

// Form đăng nhập dùng chung cho admin và pos (UC-AUTH-02). Đăng nhập xong thì useAuth() có user;
// trang chứa form dựa vào đó để chuyển đi.
export function LoginForm() {
  const { mutate, isPending, error } = useMutation({
    mutationFn: ({ identifier, password }: Values) => login(identifier.trim(), password),
  });

  return (
    <Form<Values> layout="vertical" size="large" onFinish={(values) => mutate(values)}>
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Form.Item
        name="identifier"
        label="Email hoặc số điện thoại"
        rules={[{ required: true, whitespace: true, message: 'Nhập email hoặc số điện thoại' }]}
      >
        <Input autoFocus autoComplete="username" />
      </Form.Item>
      <Form.Item name="password" label="Mật khẩu" rules={[{ required: true, message: 'Nhập mật khẩu' }]}>
        <Input.Password autoComplete="current-password" />
      </Form.Item>
      <Button type="primary" htmlType="submit" loading={isPending} block>
        Đăng nhập
      </Button>
    </Form>
  );
}
