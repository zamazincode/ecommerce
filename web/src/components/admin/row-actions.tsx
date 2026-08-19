"use client";

import Link from "next/link";
import type { LucideIcon } from "lucide-react";

import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { Tooltip, TooltipTrigger, TooltipContent } from "@/components/ui/tooltip";

interface RowActionConfirm {
	title: string;
	description?: string;
	confirmLabel?: string;
}

interface RowActionProps {
	icon: LucideIcon;
	label: string;
	/** Verilirse buton bir `<Link>` olarak render edilir (Next 16 `<a><button/></a>` hatasını önler). */
	href?: string;
	onClick?: () => void;
	tone?: "default" | "danger";
	disabled?: boolean;
	/** Geri dönüşsüz eylemler (sil/iptal) için — `window.confirm()` yerine. */
	confirm?: RowActionConfirm;
}

/**
 * Tablo satırı eylem butonu. `href` + `nativeButton={false}` kombinasyonu
 * "buton içinde link" geçersiz HTML'ini önlüyor (sorun #4, #5); `tone="danger"`
 * yıkıcı eylemi görsel olarak ayırıyor (sorun #6); `confirm` native `confirm()`'in
 * yerini alıyor (sorun #23).
 *
 * NOT: `confirm` verildiğinde buton `Tooltip` İLE SARILMIYOR — `ConfirmDialog`
 * kendi `trigger`'ını `AlertDialogTrigger`'ın `render` prop'uyla klonluyor ve bu
 * bileşen ekstra (tooltip'in ihtiyaç duyduğu) `onMouseEnter`/`onFocus`
 * prop'larını ileri taşımıyor — iç içe render kompozisyonu sessizce
 * çalışmaz hâle gelir (ölçüldü, `useRenderElement`'in `cloneElement` mantığı).
 * `aria-label` erişilebilirliği zaten sağlıyor, diyaloğun kendi başlığı da
 * hover tooltip'inin verdiği bilgiyi zaten tekrar ediyor.
 */
export function RowAction({
	icon: Icon,
	label,
	href,
	onClick,
	tone = "default",
	disabled,
	confirm,
}: RowActionProps) {
	const toneClassName =
		tone === "danger"
			? "text-destructive hover:bg-destructive/10 hover:text-destructive"
			: undefined;

	if (confirm) {
		return (
			<ConfirmDialog
				trigger={
					<Button
						variant="ghost"
						size="icon-sm"
						aria-label={label}
						disabled={disabled}
						className={toneClassName}
					>
						<Icon />
					</Button>
				}
				title={confirm.title}
				description={confirm.description}
				confirmLabel={confirm.confirmLabel}
				tone={tone === "danger" ? "danger" : "default"}
				onConfirm={() => onClick?.()}
			/>
		);
	}

	const button = href ? (
		<Button
			variant="ghost"
			size="icon-sm"
			aria-label={label}
			disabled={disabled}
			className={toneClassName}
			render={<Link href={href} />}
			nativeButton={false}
		>
			<Icon />
		</Button>
	) : (
		<Button
			variant="ghost"
			size="icon-sm"
			aria-label={label}
			disabled={disabled}
			className={toneClassName}
			onClick={onClick}
		>
			<Icon />
		</Button>
	);

	return (
		<Tooltip>
			<TooltipTrigger render={button} />
			<TooltipContent>{label}</TooltipContent>
		</Tooltip>
	);
}

export function RowActions({
	children,
	className,
}: {
	children: React.ReactNode;
	className?: string;
}) {
	return (
		<div className={cn("flex items-center justify-end gap-1", className)}>
			{children}
		</div>
	);
}
