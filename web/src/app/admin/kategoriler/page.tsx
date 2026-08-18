"use client";

import { useAdminCategories } from "@/hooks/use-admin-categories";
import { CategoryFormDialog } from "@/components/admin/category-form-dialog";
import { categoryDepth } from "@/lib/admin/category-tree";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
	Table,
	TableBody,
	TableCell,
	TableHead,
	TableHeader,
	TableRow,
} from "@/components/ui/table";
import { Skeleton } from "@/components/ui/skeleton";

export default function AdminCategoriesPage() {
	const { data: categories, isLoading } = useAdminCategories();

	return (
		<div>
			<div className="mb-6 flex items-center justify-between">
				<h1 className="text-xl font-semibold">Kategoriler</h1>
				{categories ? (
					<CategoryFormDialog
						categories={categories}
						trigger={<Button>Yeni Kategori</Button>}
					/>
				) : null}
			</div>

			{isLoading || !categories ? (
				<Skeleton className="h-64 w-full" />
			) : (
				<Table>
					<TableHeader>
						<TableRow>
							<TableHead>Ad</TableHead>
							<TableHead>Ürün Sayısı</TableHead>
							<TableHead>Durum</TableHead>
							<TableHead className="text-right">İşlemler</TableHead>
						</TableRow>
					</TableHeader>
					<TableBody>
						{categories.map((category) => (
							<TableRow key={category.id}>
								<TableCell>
									{"— ".repeat(categoryDepth(category, categories))}
									{category.name}
								</TableCell>
								<TableCell>{category.productCount}</TableCell>
								<TableCell>
									<Badge
										variant={
											category.isActive ? "default" : "secondary"
										}
									>
										{category.isActive ? "Aktif" : "Pasif"}
									</Badge>
								</TableCell>
								<TableCell className="text-right">
									<CategoryFormDialog
										category={category}
										categories={categories}
										trigger={
											<Button variant="ghost" size="sm">
												Düzenle
											</Button>
										}
									/>
								</TableCell>
							</TableRow>
						))}
					</TableBody>
				</Table>
			)}
		</div>
	);
}
