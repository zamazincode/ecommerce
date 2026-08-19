import { cn } from "@/lib/utils";

interface PageHeaderProps {
	title: string;
	description?: string;
	breadcrumb?: React.ReactNode;
	actions?: React.ReactNode;
	className?: string;
}

/**
 * 7 admin sayfasının ortak başlık bloğu — her sayfa kendi `<h1>`'ini
 * tekrarlamak yerine bunu kullanıyor (Faz D, sorun #26).
 */
export function PageHeader({
	title,
	description,
	breadcrumb,
	actions,
	className,
}: PageHeaderProps) {
	return (
		<div
			className={cn(
				"flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between",
				className,
			)}
		>
			<div className="min-w-0">
				{breadcrumb}
				<h1 className="font-heading text-2xl font-semibold tracking-tight">
					{title}
				</h1>
				{description ? (
					<p className="mt-1 text-sm text-muted-foreground">
						{description}
					</p>
				) : null}
			</div>
			{actions ? (
				<div className="flex shrink-0 items-center gap-2">{actions}</div>
			) : null}
		</div>
	);
}
