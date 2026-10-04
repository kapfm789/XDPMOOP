import { ApiError, errorMessage, registerTenant, useAuth } from '@oism/shared';
import { useMutation } from '@tanstack/react-query';
import { Alert, Button, Form, Input } from 'antd';

type Values = { tenantName: string; ownerName: string; email?: string; phone?: string; password: string };

const DUPLICATE_MESSAGE = 'Email hoặc số điện thoại đã được dùng';

// UC-AUTH-01: tạo tenant kèm tài khoản Owner, rồi đăng nhập luôn bằng tài khoản đó.
export function RegisterTenantForm() {
  const [form] = Form.useForm<Values>();
  const { login } = useAuth();

  const { mutate, isPending, error } = useMutation({
    mutationFn: async ({ email, phone, ...rest }: Values) => {
      const identifier = email?.trim() || phone?.trim() || '';
      await registerTenant({ ...rest, email: email?.trim() || undefined, phone: phone?.trim() || undefined });
      await login(identifier, rest.password);
    },
    onError: (failure) => {
      // Backend trả lỗi theo từng trường: hiện cạnh ô tương ứng.
      if (failure instanceof ApiError && failure.code === 'validation_failed') {
        form.setFields(Object.entries(failure.errors).map(([name, errors]) => ({ name: name as keyof Values, errors })));
      }
    },
  });

  const summary =
    error instanceof ApiError && error.code === 'duplicate' ? DUPLICATE_MESSAGE : error ? errorMessage(error) : null;

  return (
    <Form<Values> form={form} layout="vertical" onFinish={(values) => mutate(values)}>
      {summary && <Alert type="error" showIcon title={summary} style={{ marginBottom: 16 }} />}
      <Form.Item name="tenantName" label="Tên cửa hàng" rules={[{ required: true, whitespace: true, message: 'Nhập tên cửa hàng' }]}>
        <Input autoFocus />
      </Form.Item>
      <Form.Item name="ownerName" label="Họ tên chủ cửa hàng" rules={[{ required: true, whitespace: true, message: 'Nhập họ tên' }]}>
        <Input autoComplete="name" />
      </Form.Item>
      <Form.Item name="email" label="Email" rules={[{ type: 'email', message: 'Email không hợp lệ' }]}>
        <Input autoComplete="email" />
      </Form.Item>
      <Form.Item
        name="phone"
        label="Số điện thoại"
        dependencies={['email']}
        rules={[
          ({ getFieldValue }) => ({
            validator: (_, phone?: string) =>
              phone?.trim() || (getFieldValue('email') as string | undefined)?.trim()
                ? Promise.resolve()
                : Promise.reject(new Error('Nhập ít nhất email hoặc số điện thoại')),
          }),
        ]}
      >
        <Input autoComplete="tel" />
      </Form.Item>
      <Form.Item
        name="password"
        label="Mật khẩu"
        rules={[
          { required: true, message: 'Nhập mật khẩu' },
          { min: 8, message: 'Mật khẩu tối thiểu 8 ký tự' },
        ]}
      >
        <Input.Password autoComplete="new-password" />
      </Form.Item>
      <Button type="primary" htmlType="submit" loading={isPending} block>
        Đăng ký
      </Button>
    </Form>
  );
}
