import Logo from "../common/logo";

export default function Header() {
	return (
		<div>
			<header className="container-x">
				<div className="flex items-end justify-between gap-4">
					upper navlinks
				</div>

				<div className="flex items-center justify-between gap-12">
					<Logo />
					<div>search bar</div>
					<div>buttons</div>
				</div>

				<nav>navlinks</nav>
			</header>
		</div>
	);
}
