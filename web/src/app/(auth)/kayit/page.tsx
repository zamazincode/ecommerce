"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter } from "next/navigation";
import { useState } from "react";
import Link from "next/link";
import {
	AlertCircleIcon,
	ArrowRightIcon,
	EyeIcon,
	EyeOffIcon,
} from "lucide-react";
import { registerSchema, type RegisterInput } from "@/lib/validations/auth";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { FormField } from "@/components/ui/form-field";
import { Separator } from "@/components/ui/separator";
import Logo from "@/components/common/logo";

export default function RegisterPage() {
	const router = useRouter();
	const [serverError, setServerError] = useState<string | null>(null);
	const [showPassword, setShowPassword] = useState(false);

	const {
		register,
		handleSubmit,
		formState: { errors, isSubmitting },
	} = useForm<RegisterInput>({
		resolver: zodResolver(registerSchema),
	});

	async function onSubmit(input: RegisterInput) {
		setServerError(null);

		const response = await fetch("/api/auth/register", {
			method: "POST",
			headers: { "Content-Type": "application/json" },
			body: JSON.stringify(input),
		});

		if (!response.ok) {
			const body = await response.json().catch(() => null);
			setServerError(
				body?.detail ?? body?.message ?? "Kayıt oluşturulamadı.",
			);
			return;
		}

		router.push("/");
		router.refresh();
	}

	return (
		<div className="w-full max-w-md rounded-2xl border bg-card p-8 shadow-card">
			<Link href="/" className="mb-6 flex justify-center">
				<Logo />
			</Link>

			<div className="mb-6 text-center">
				<h1 className="text-lg font-semibold text-primary">
					D&R Kültür, Sanat ve Eğlence Dünyası
				</h1>
				<p className="mt-2 text-sm text-muted-foreground">
					Giriş yap ya da hemen üye ol; kültür, sanat ve eğlence
					dolu alışveriş deneyimini keşfet.
				</p>
			</div>

			<form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
				{serverError ? (
					<p className="flex items-center gap-2 rounded-lg bg-destructive/10 p-3 text-sm text-destructive">
						<AlertCircleIcon className="size-4 shrink-0" />
						{serverError}
					</p>
				) : null}

				<div className="grid grid-cols-2 gap-4">
					<FormField
						label="Ad"
						htmlFor="firstName"
						error={errors.firstName?.message}
					>
						<Input id="firstName" {...register("firstName")} />
					</FormField>
					<FormField
						label="Soyad"
						htmlFor="lastName"
						error={errors.lastName?.message}
					>
						<Input id="lastName" {...register("lastName")} />
					</FormField>
				</div>

				<FormField
					label="E-posta"
					htmlFor="email"
					error={errors.email?.message}
				>
					<Input id="email" type="email" {...register("email")} />
				</FormField>

				<FormField
					label="Şifre"
					htmlFor="password"
					error={errors.password?.message}
					hint="En az 8 karakter, büyük/küçük harf ve rakam içermeli."
				>
					<div className="relative">
						<Input
							id="password"
							type={showPassword ? "text" : "password"}
							{...register("password")}
							className="pr-10"
						/>
						<Button
							type="button"
							variant="ghost"
							size="icon-sm"
							className="absolute top-1 right-1 bottom-1 h-auto text-muted-foreground"
							onClick={() => setShowPassword((prev) => !prev)}
							aria-label={
								showPassword ? "Şifreyi gizle" : "Şifreyi göster"
							}
						>
							{showPassword ? <EyeOffIcon /> : <EyeIcon />}
						</Button>
					</div>
				</FormField>

				<Button type="submit" className="w-full" disabled={isSubmitting}>
					{isSubmitting ? "Oluşturuluyor…" : "Hesap Oluştur"}
					<ArrowRightIcon />
				</Button>
			</form>

			<Separator className="my-6" />

			<p className="text-center text-sm text-muted-foreground">
				Zaten hesabın var mı?{" "}
				<Link
					href="/giris"
					className="font-medium text-primary hover:underline"
				>
					Giriş yap
				</Link>
			</p>
		</div>
	);
}
